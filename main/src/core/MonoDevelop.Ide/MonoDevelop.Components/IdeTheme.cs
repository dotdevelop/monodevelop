﻿//
// ThemeExtensions.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.IO;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using System.Linq;

#if MAC
using AppKit;
using Foundation;
using MonoDevelop.Components.Mac;
#endif

namespace MonoDevelop.Components
{
	public static class IdeTheme
	{
		internal static string DefaultTheme;
		internal static string DefaultGtkDataFolder;
		internal static string DefaultGtk2RcFiles;

		public static Skin UserInterfaceSkin { get; private set; }

		static IdeTheme ()
		{
			DefaultGtkDataFolder = Environment.GetEnvironmentVariable ("GTK_DATA_PREFIX");
			DefaultGtk2RcFiles = Environment.GetEnvironmentVariable ("GTK2_RC_FILES");
			IdeApp.Preferences.UserInterfaceTheme.Changed += (sender, e) => UpdateGtkTheme ();
		}

		internal static void InitializeGtk (string progname, ref string[] args)
		{
			if (Gtk.Settings.Default != null)
				throw new InvalidOperationException ("Gtk already initialized!");
			
			//HACK: we must initilize some Gtk rc before Gtk.Application is initialized on Mac/Windows
			//      otherwise it will not be loaded correctly and theme switching won't work.
			if (!Platform.IsLinux)
				UpdateGtkTheme ();

			Gtk.Application.Init (BrandingService.ApplicationName, ref args);
		}

		internal static void SetupXwtTheme ()
		{
			Xwt.Drawing.Context.RegisterStyles ("dark", "disabled");

			#if MAC
			Xwt.Drawing.Context.RegisterStyles ("sel");
			#endif

			Xwt.Toolkit.CurrentEngine.RegisterBackend <Xwt.Backends.IWindowBackend, ThemedGtkWindowBackend>();
			Xwt.Toolkit.CurrentEngine.RegisterBackend <Xwt.Backends.IDialogBackend, ThemedGtkDialogBackend>();
		}

		internal static void SetupGtkTheme ()
		{
			if (Gtk.Settings.Default == null)
				return;
			
			if (Platform.IsLinux) {
				DefaultTheme = Gtk.Settings.Default.ThemeName;
				string theme = IdeApp.Preferences.UserInterfaceTheme;
				if (string.IsNullOrEmpty (theme))
					theme = DefaultTheme;
				ValidateGtkTheme (ref theme);
				if (theme != DefaultTheme)
					Gtk.Settings.Default.ThemeName = theme;
				LoggingService.LogInfo ("GTK: Using Gtk theme from {0}", Path.Combine (Gtk.Rc.ThemeDir, Gtk.Settings.Default.ThemeName));
			} else
				DefaultTheme = "Light";

			// HACK: on Windows we have to load the theme twice on startup. During the first run we
			//       set the environment variables from InitializeGtk() and after Gtk initialization
			//       we set the active theme from here. Otherwise Gtk will preload the default theme with
			//       the Wimp engine, which can break our own configs.
			if (Platform.IsWindows)
				UpdateGtkTheme ();
		}

		internal static void UpdateGtkTheme ()
		{
			if (DefaultTheme == null)
				SetupGtkTheme ();

			if (!Platform.IsLinux)
				UserInterfaceSkin = IdeApp.Preferences.UserInterfaceTheme == "Dark" ? Skin.Dark : Skin.Light;

			var current_theme = IdeApp.Preferences.UserInterfaceTheme;
			var use_bundled_theme = false;

			
			// Use the bundled gtkrc only if the Xamarin theme is installed
			if (File.Exists (Path.Combine (Gtk.Rc.ModuleDir, "libxamarin.so")) || File.Exists (Path.Combine (Gtk.Rc.ModuleDir, "libxamarin.dll")))
				use_bundled_theme = true;
			
			if (use_bundled_theme) {
				
				if (!Directory.Exists (UserProfile.Current.ConfigDir))
					Directory.CreateDirectory (UserProfile.Current.ConfigDir);
				
				if (Platform.IsWindows) {
					// HACK: Gtk Bug: Rc.ReparseAll () and the include "[rcfile]" gtkrc statement are broken on Windows.
					//                We must provide our own XDG folder structure to switch bundled themes.
					var rc_themes = UserProfile.Current.ConfigDir.Combine ("share", "themes");
					var rc_theme_light = rc_themes.Combine ("Light", "gtk-2.0", "gtkrc");
					var rc_theme_dark = rc_themes.Combine ("Dark", "gtk-2.0", "gtkrc");
					if (!Directory.Exists (rc_theme_light.ParentDirectory))
						Directory.CreateDirectory (rc_theme_light.ParentDirectory);
					if (!Directory.Exists (rc_theme_dark.ParentDirectory))
						Directory.CreateDirectory (rc_theme_dark.ParentDirectory);

					string gtkrc = PropertyService.EntryAssemblyPath.Combine ("gtkrc");
					File.Copy (gtkrc + ".win32", rc_theme_light, true);
					File.Copy (gtkrc + ".win32-dark", rc_theme_dark, true);

					Environment.SetEnvironmentVariable ("GTK_DATA_PREFIX", UserProfile.Current.ConfigDir);

					// set the actual theme and reset the environment only after Gtk has been fully
					// initialized. See SetupGtkTheme ().
					if (Gtk.Settings.Default != null) {
						LoggingService.LogInfo ("GTK: Using Gtk theme from {0}", Path.Combine (Gtk.Rc.ThemeDir, current_theme));
						Gtk.Settings.Default.ThemeName = current_theme;
						Environment.SetEnvironmentVariable ("GTK_DATA_PREFIX", DefaultGtkDataFolder);
					}

				} else if (Platform.IsMac) {
					
					var gtkrc = "gtkrc.mac";
					if (IdeApp.Preferences.UserInterfaceSkin == Skin.Dark)
						gtkrc += "-dark";
					gtkrc = PropertyService.EntryAssemblyPath.Combine (gtkrc);

					LoggingService.LogInfo ("GTK: Using gtkrc from {0}", gtkrc);
					
					// Generate a dummy rc file and use that to include the real rc. This allows changing the rc
					// on the fly. All we have to do is rewrite the dummy rc changing the include and call ReparseAll
					var rcFile = UserProfile.Current.ConfigDir.Combine ("gtkrc");
					File.WriteAllText (rcFile, "include \"" + gtkrc + "\"");
					Environment.SetEnvironmentVariable ("GTK2_RC_FILES", rcFile);

					Gtk.Rc.ReparseAll ();
				}

			} else if (Gtk.Settings.Default != null && current_theme != Gtk.Settings.Default.ThemeName) {
				LoggingService.LogInfo ("GTK: Using Gtk theme from {0}", Path.Combine (Gtk.Rc.ThemeDir, current_theme));
				Gtk.Settings.Default.ThemeName = current_theme;
			}

			// let Gtk realize the new theme
			// Style is being updated by DefaultWorkbench.OnStyleSet ()
			// This ensures that the theme and all styles have been loaded when
			// the Styles.Changed event is raised.
			//GLib.Timeout.Add (50, delegate { UpdateStyles(); return false; });
		}

		internal static void UpdateStyles ()
		{
			if (Platform.IsLinux) {
				var defaultStyle = Gtk.Rc.GetStyle (IdeApp.Workbench.RootWindow);
				var bgColor = defaultStyle.Background (Gtk.StateType.Normal);
				UserInterfaceSkin = HslColor.Brightness (bgColor) < 0.5 ? Skin.Dark : Skin.Light;
			}

			if (UserInterfaceSkin == Skin.Dark)
				Xwt.Drawing.Context.SetGlobalStyle ("dark");
			else
				Xwt.Drawing.Context.ClearGlobalStyle ("dark");

			Styles.LoadStyle ();
			#if MAC
			UpdateMacWindows ();
			#endif
		}

		internal static string[] gtkThemeFallbacks = new string[] {
			"Xamarin",// the best!
			"Gilouche", // SUSE
			"Mint-X", // MINT
			"Radiance", // Ubuntu 'light' theme (MD looks better with the light theme in 4.0 - if that changes switch this one)
			"Clearlooks" // GTK theme
		};

		static void ValidateGtkTheme (ref string theme)
		{
			if (!MonoDevelop.Ide.Gui.OptionPanels.IDEStyleOptionsPanelWidget.IsBadGtkTheme (theme))
				return;

			var themes = MonoDevelop.Ide.Gui.OptionPanels.IDEStyleOptionsPanelWidget.InstalledThemes;

			string fallback = gtkThemeFallbacks
				.Select (fb => themes.FirstOrDefault (t => string.Compare (fb, t, StringComparison.OrdinalIgnoreCase) == 0))
				.FirstOrDefault (t => t != null);

			string message = "Theme Not Supported";

			string detail;
			if (themes.Count > 0) {
				detail =
					"Your system is using the '{0}' GTK+ theme, which is known to be very unstable. MonoDevelop will " +
					"now switch to an alternate GTK+ theme.\n\n" +
					"This message will continue to be shown at startup until you set a alternate GTK+ theme as your " +
					"default in the GTK+ Theme Selector or MonoDevelop Preferences.";
			} else {
				detail =
					"Your system is using the '{0}' GTK+ theme, which is known to be very unstable, and no other GTK+ " +
					"themes appear to be installed. Please install another GTK+ theme.\n\n" +
					"This message will continue to be shown at startup until you install a different GTK+ theme and " +
					"set it as your default in the GTK+ Theme Selector or MonoDevelop Preferences.";
			}

			MessageService.GenericAlert (Gtk.Stock.DialogWarning, message, BrandingService.BrandApplicationName (detail), AlertButton.Ok);

			theme = fallback ?? themes.FirstOrDefault () ?? theme;
		}

#if MAC
		static Dictionary<NSWindow, NSObject> nsWindows = new Dictionary<NSWindow, NSObject> ();

		public static void ApplyTheme (NSWindow window)
		{
			if (!nsWindows.ContainsKey(window)) {
				nsWindows [window] = NSNotificationCenter.DefaultCenter.AddObserver (NSWindow.WillCloseNotification, OnClose, window);
				SetTheme (window);
			}
		}

		static void SetTheme (NSWindow window)
		{
			if (IdeApp.Preferences.UserInterfaceSkin == Skin.Light)
				window.Appearance = NSAppearance.GetAppearance (NSAppearance.NameAqua);
			else
				window.Appearance = NSAppearance.GetAppearance (NSAppearance.NameVibrantDark);

			if (window is NSPanel)
				window.BackgroundColor = MonoDevelop.Ide.Gui.Styles.BackgroundColor.ToNSColor ();
			else {
				object[] platforms = Mono.Addins.AddinManager.GetExtensionObjects ("/MonoDevelop/Core/PlatformService");
				if (platforms.Length > 0) {
					var platformService = (MonoDevelop.Ide.Desktop.PlatformService)platforms [0];
					var image = Xwt.Drawing.Image.FromResource (platformService.GetType().Assembly, "maintoolbarbg.png");

					window.IsOpaque = false;
					window.BackgroundColor = NSColor.FromPatternImage (image.ToBitmap().ToNSImage());
				}
				if (window.ContentView.Class.Name != "GdkQuartzView") {
					window.ContentView.WantsLayer = true;
					window.ContentView.Layer.BackgroundColor = MonoDevelop.Ide.Gui.Styles.BackgroundColor.ToCGColor ();
				}
			}
			window.StyleMask |= NSWindowStyle.TexturedBackground;
		}

		static void OnClose (NSNotification note)
		{
			var w = (NSWindow)note.Object;
			NSNotificationCenter.DefaultCenter.RemoveObserver(nsWindows[w]);
			nsWindows.Remove (w);
		}

		static void UpdateMacWindows ()
		{
			foreach (var w in nsWindows.Keys)
				SetTheme (w);
		}

		static void OnGtkWindowRealized (object s, EventArgs a)
		{
			var nsw = MonoDevelop.Components.Mac.GtkMacInterop.GetNSWindow ((Gtk.Window) s);
			if (nsw != null)
				ApplyTheme (nsw);
		}
#endif

		public static void ApplyTheme (this Gtk.Window window)
		{
			#if MAC
			window.Realized += OnGtkWindowRealized;
			if (window.IsRealized) {
				var nsw = MonoDevelop.Components.Mac.GtkMacInterop.GetNSWindow (window);
				if (nsw != null)
					ApplyTheme (nsw);
			}
			#endif
		}
	}

	public class ThemedGtkWindowBackend : Xwt.GtkBackend.WindowBackend
	{
		public override void Initialize ()
		{
			base.Initialize ();
			IdeTheme.ApplyTheme (Window);
		}
	}

	public class ThemedGtkDialogBackend : Xwt.GtkBackend.DialogBackend
	{
		public override void Initialize ()
		{
			base.Initialize ();
			IdeTheme.ApplyTheme (Window);
		}
	}
}

