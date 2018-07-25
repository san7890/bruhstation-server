﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Tgstation.Server.Host.Startup;

namespace Tgstation.Server.Host.Watchdog
{
	/// <summary>
	/// <see cref="IServerFactory"/> for loading <see cref="IServer"/>s in a different <see cref="AssemblyLoadContext"/>
	/// </summary>
	sealed class IsolatedServerFactory : AssemblyLoadContext, IServerFactory
	{
		const string DllExtension = "dll";

		static readonly string assemblyFileName = String.Join(".", nameof(Tgstation), nameof(Server), nameof(Host), DllExtension);

		/// <summary>
		/// The path of the <see cref="Assembly"/> to load
		/// </summary>
		readonly string assemblyPath;

		/// <summary>
		/// Construct a <see cref="IsolatedServerFactory"/>
		/// </summary>
		/// <param name="assemblyPath">The value of <see cref="assemblyPath"/></param>
		public IsolatedServerFactory(string assemblyPath)
		{
			this.assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));

			Resolving += IsolatedServerFactory_Resolving;
		}

		//https://stackoverflow.com/a/40921746/3976486
		Assembly IsolatedServerFactory_Resolving(AssemblyLoadContext context, AssemblyName assemblyName)
		{
			if (assemblyName.Name.EndsWith("resources", StringComparison.Ordinal))
				return null;

			var foundDll = Directory.GetFileSystemEntries(assemblyPath, String.Join(".", assemblyName.Name, DllExtension), SearchOption.AllDirectories).FirstOrDefault();
			if (foundDll != default)
				return context.LoadFromAssemblyPath(foundDll);
			return context.LoadFromAssemblyName(assemblyName);
		}

		/// <summary>
		/// Loads the <see cref="Assembly"/> at <see cref="assemblyPath"/> and creates an <see cref="IServer"/> from it
		/// </summary>
		/// <param name="args">The arguments for the <see cref="IServer"/></param>
		/// <param name="updatePath">The updatePath for the <see cref="IServer"/></param>
		/// <returns>A new <see cref="IServer"/></returns>
		public IServer CreateServer(string[] args, string updatePath)
		{
			var assembly = LoadFromAssemblyPath(Path.Combine(assemblyPath, assemblyFileName));

			//find the IServerFactory implementation
			var serverFactoryInterfaceType = typeof(IServerFactory);
			var serverFactoryImplementationType = assembly.GetTypes().Where(x => serverFactoryInterfaceType.IsAssignableFrom(x)).First();

			var serverFactory = (IServerFactory)Activator.CreateInstance(serverFactoryImplementationType);

			return serverFactory.CreateServer(args, updatePath);
		}

		//honestly have no idea what this is for, https://github.com/dotnet/coreclr/blob/master/Documentation/design-docs/assemblyloadcontext.md
		/// <inheritdoc />
		protected override Assembly Load(AssemblyName assemblyName) => null;
	}
}
