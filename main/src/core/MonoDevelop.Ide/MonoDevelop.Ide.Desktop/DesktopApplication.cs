//
// DesktopApplication.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2006 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace MonoDevelop.Ide.Desktop
{
	[Flags]
	public enum DesktopApplicationRole
	{
		None   = 0,
		Viewer = 1,
		Editor = 2,
		Shell  = 4,
		All    = Viewer | Editor | Shell
	}

	public abstract class DesktopApplication
	{
		public DesktopApplication (string id, string displayName, bool isDefault)
		{
			if (string.IsNullOrEmpty (displayName))
				throw new ArgumentException ("displayName cannot be empty");
			if (string.IsNullOrEmpty (id))
				throw new ArgumentException ("id cannot be empty");
			this.DisplayName = displayName;
			this.Id = id;
			this.IsDefault = isDefault;
		}

		public string DisplayName { get; private set; }
		
		/// <summary>
		/// Used to uniquely identify the application or command.
		/// </summary>
		public string Id { get; private set; }
		
		public bool IsDefault { get; private set; }
		
		public abstract void Launch (params string[] files);
		
		public override int GetHashCode ()
		{
			return Id.GetHashCode ();
		}
		
		public override bool Equals (object obj)
		{
			var other = obj as DesktopApplication;
			return other != null && other.Id == Id;
		}
	}
}
