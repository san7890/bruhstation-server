﻿using System;
using System.Windows.Forms;
using TGServiceInterface;

namespace TGControlPanel
{
	partial class Main
	{
		string lastReadError = null;
		void InitBYONDPage()
		{
			var BYOND = Server.GetComponent<ITGByond>();
			var CV = BYOND.GetVersion(TGByondVersion.Installed);
			if (CV == null)
				CV = BYOND.GetVersion(TGByondVersion.Staged);
			if (CV != null)
			{
				var splits = CV.Split('.');
				if(splits.Length == 2)
				{
					try
					{
						var Major = Convert.ToInt32(splits[0]);
						var Minor = Convert.ToInt32(splits[1]);
						MajorVersionNumeric.Value = Major;
						MinorVersionNumeric.Value = Minor;
					}
					catch { }
				}
			}

			var latestVer = Server.GetComponent<ITGByond>().GetVersion(TGByondVersion.Latest);
			LatestVersionLabel.Text = latestVer;

			try
			{
				var splits = latestVer.Split('.');
				var maj = Convert.ToInt32(splits[0]);
				MinorVersionNumeric.Value = Convert.ToInt32(splits[1]);
				MajorVersionNumeric.Value = maj;
			}
			catch { }

			UpdateBYONDButtons();
		}
		private void UpdateButton_Click(object sender, EventArgs e)
		{
			UpdateBYONDButtons();
			if (!Server.GetComponent<ITGByond>().UpdateToVersion((int)MajorVersionNumeric.Value, (int)MinorVersionNumeric.Value))
				MessageBox.Show("Unable to begin update, there is another operation in progress.");
		}
		
		private void BYONDRefreshButton_Click(object sender, EventArgs e)
		{
			UpdateBYONDButtons();
		}
		void UpdateBYONDButtons()
		{
			var BYOND = Server.GetComponent<ITGByond>();

			VersionLabel.Text = BYOND.GetVersion(TGByondVersion.Installed) ?? "Not Installed";

			StagedVersionTitle.Visible = false;
			StagedVersionLabel.Visible = false;
			switch (BYOND.CurrentStatus())
			{
				case TGByondStatus.Idle:
				case TGByondStatus.Starting:
					StatusLabel.Text = "Idle";
					UpdateButton.Enabled = true;
					break;
				case TGByondStatus.Downloading:
					StatusLabel.Text = "Downloading...";
					UpdateButton.Enabled = false;
					break;
				case TGByondStatus.Staging:
					StatusLabel.Text = "Staging...";
					UpdateButton.Enabled = false;
					break;
				case TGByondStatus.Staged:
					StagedVersionTitle.Visible = true;
					StagedVersionLabel.Visible = true;
					StagedVersionLabel.Text = BYOND.GetVersion(TGByondVersion.Staged) ?? "Unknown";
					StatusLabel.Text = "Staged and waiting for BYOND to shutdown...";
					UpdateButton.Enabled = true;
					break;
				case TGByondStatus.Updating:
					StatusLabel.Text = "Applying update...";
					UpdateButton.Enabled = false;
					break;
			}
			var error = Server.GetComponent<ITGByond>().GetError();
			if (error != lastReadError)
			{
				lastReadError = error;
				if (error != null)
					MessageBox.Show("An error occurred: " + lastReadError);
			}
		}
	}
}
