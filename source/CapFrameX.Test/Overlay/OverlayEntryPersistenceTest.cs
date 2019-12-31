﻿using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.IO;

namespace CapFrameX.Test.Overlay
{
	[TestClass]
	public class OverlayEntryPersistenceTest
	{
		[TestMethod]
		public void CreateJsonFile_InitialFile()
		{
			var persistence = new OverlayEntryPersistence
			{
				OverlayEntries = new System.Collections.Generic.List<OverlayEntryWrapper>
				{
					// CX 
					// CaptureServiceStatus
					new OverlayEntryWrapper("CaptureServiceStatus")
					{
						ShowOnOverlay = true,
						Description = "Capture service status",
						GroupName = string.Empty,
						Value = "Capture service ready...",
						ShowGraph = false,
						Color = string.Empty
					},

					// RunHistory
					new OverlayEntryWrapper("RunHistory")
					{
						ShowOnOverlay = true,
						Description = "Run history",
						GroupName = string.Empty,
						Value = default(object),
						ShowGraph = false,
						Color = string.Empty
					},

					// RunHistory
					new OverlayEntryWrapper("CaptureTimer")
					{
						ShowOnOverlay = true,
						Description = "Capture timer",
						GroupName = "Timer: ",
						Value = "0",
						ShowGraph = false,
						Color = string.Empty
					},

					// RTSS
					// Framerate
					new OverlayEntryWrapper("Framerate")
					{
						ShowOnOverlay = true,
						Description = "Framerate",
						GroupName = "<APP>",
						Value = 0d,
						ShowGraph = false,
						Color = string.Empty
					},

					// Frametime
					new OverlayEntryWrapper("Frametime")
					{
						ShowOnOverlay = true,
						Description = "Frametime",
						GroupName = "<APP>",
						Value = 0d,
						ShowGraph = true,
						Color = string.Empty
					}
				}
			};

			File.WriteAllText(
				@"..\..\..\..\CapFrameX.Overlay\OverlayConfiguration\OverlayEntryConfiguration.json", 
				JsonConvert.SerializeObject(persistence));
		}
	}
}
