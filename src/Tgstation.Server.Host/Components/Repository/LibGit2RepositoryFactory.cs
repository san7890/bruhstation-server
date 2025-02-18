﻿using System;
using System.Threading;
using System.Threading.Tasks;

using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;

using Tgstation.Server.Api.Models;
using Tgstation.Server.Host.IO;
using Tgstation.Server.Host.Jobs;

namespace Tgstation.Server.Host.Components.Repository
{
	/// <inheritdoc />
	sealed class LibGit2RepositoryFactory : ILibGit2RepositoryFactory
	{
		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="LibGit2RepositoryFactory"/>.
		/// </summary>
		readonly ILogger<LibGit2RepositoryFactory> logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="LibGit2RepositoryFactory"/> class.
		/// </summary>
		/// <param name="logger">The value of <see cref="logger"/>.</param>
		public LibGit2RepositoryFactory(ILogger<LibGit2RepositoryFactory> logger)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <inheritdoc />
		public void CreateInMemory()
		{
			logger.LogTrace("Creating in-memory libgit2 repository...");
			using (new LibGit2Sharp.Repository())
				logger.LogTrace("Success");
		}

		/// <inheritdoc />
		public async Task<LibGit2Sharp.IRepository> CreateFromPath(string path, CancellationToken cancellationToken)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));

			var repo = await Task.Factory.StartNew<LibGit2Sharp.IRepository>(
				() =>
				{
					logger.LogTrace("Creating libgit2 repostory at {0}...", path);
					return new LibGit2Sharp.Repository(path);
				},
				cancellationToken,
				DefaultIOManager.BlockingTaskCreationOptions,
				TaskScheduler.Current)
				.ConfigureAwait(false);

			return repo;
		}

		/// <inheritdoc />
		public Task Clone(Uri url, CloneOptions cloneOptions, string path, CancellationToken cancellationToken) => Task.Factory.StartNew(
			() =>
			{
				try
				{
					logger.LogTrace("Cloning {0} into {1}...", url, path);
					LibGit2Sharp.Repository.Clone(url.ToString(), path, cloneOptions);
				}
				catch (UserCancelledException ex)
				{
					logger.LogTrace(ex, "Suppressing clone cancellation exception");
					cancellationToken.ThrowIfCancellationRequested();
				}
				catch (LibGit2SharpException ex)
				{
					CheckBadCredentialsException(ex);
					throw;
				}
			},
			cancellationToken,
			DefaultIOManager.BlockingTaskCreationOptions,
			TaskScheduler.Current);

		/// <inheritdoc />
		public CredentialsHandler GenerateCredentialsHandler(string username, string password) => (a, b, supportedCredentialTypes) =>
		{
			var hasCreds = username != null;
			var supportsUserPass = supportedCredentialTypes.HasFlag(SupportedCredentialTypes.UsernamePassword);
			var supportsAnonymous = supportedCredentialTypes.HasFlag(SupportedCredentialTypes.Default);

			logger.LogTrace("Credentials requested. Present: {0}. Supports anonymous: {1}. Supports user/pass: {2}", hasCreds, supportsAnonymous, supportsUserPass);
			if (supportsUserPass && hasCreds)
				return new UsernamePasswordCredentials
				{
					Username = username,
					Password = password,
				};

			if (supportsAnonymous)
				return new DefaultCredentials();

			if (supportsUserPass)
				throw new JobException(ErrorCode.RepoCredentialsRequired);

			throw new JobException(ErrorCode.RepoCannotAuthenticate);
		};

		/// <inheritdoc />
		public void CheckBadCredentialsException(LibGit2SharpException exception)
		{
			if (exception == null)
				throw new ArgumentNullException(nameof(exception));

			if (exception.Message == "too many redirects or authentication replays")
				throw new JobException("Bad git credentials exchange!", exception);

			if (exception.Message == ErrorCode.RepoCredentialsRequired.Describe())
				throw new JobException(ErrorCode.RepoCredentialsRequired);

			// submodule recursion
			if (exception.InnerException is LibGit2SharpException innerException)
				CheckBadCredentialsException(innerException);
		}
	}
}
