// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;

namespace Mono.CompilerServices.SymbolWriter
{
	internal static class PclFileAccess
	{
		private static Lazy<Func<string, Stream>> lazyFileOpenStreamMethod = new Lazy<Func<string, Stream>> (() => {
			Type file;
			try {
				// try contract name first:
				file = Type.GetType ("System.IO.File, System.IO.FileStream, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", throwOnError: false);
			} catch {
				file = null;
			}

			if (file == null) {
				try {
					// try corlib next:
					file = typeof(object).GetTypeInfo ().Assembly.GetType ("System.IO.File");
				} catch {
					file = null;
				}
			}

			try {
				var openRead = file.GetTypeInfo ().GetDeclaredMethod ("OpenRead");
				return (Func<string, Stream>)openRead.CreateDelegate (typeof(Func<string, Stream>));
			} catch {
				return null;
			}
		});

		internal static Stream OpenFileStream (string path)
		{
			var factory = lazyFileOpenStreamMethod.Value;
			if (factory == null) {
				throw new PlatformNotSupportedException ();
			}

			Stream fileStream;
			try {
				fileStream = factory (path);
			} catch (ArgumentException) {
				throw;
			} catch (IOException e) {
				if (e.GetType ().Name == "DirectoryNotFoundException") {
					throw new FileNotFoundException (e.Message, path, e);
				}

				throw;
			} catch (Exception e) {
				throw new IOException (e.Message, e);
			}

			return fileStream;
		}

		private static Lazy<Func<string, Stream>> lazyFileCreateStreamMethod = new Lazy<Func<string, Stream>> (() => {
			Type file;
			try {
				// try contract name first:
				file = Type.GetType ("System.IO.File, System.IO.FileStream, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", throwOnError: false);
			} catch {
				file = null;
			}

			if (file == null) {
				try {
					// try corlib next:
					file = typeof(object).GetTypeInfo ().Assembly.GetType ("System.IO.File");
				} catch {
					file = null;
				}
			}

			try {
				var openWrite = file.GetTypeInfo ().GetDeclaredMethod ("OpenWrite");
				return (Func<string, Stream>)openWrite.CreateDelegate (typeof(Func<string, Stream>));
			} catch {
				return null;
			}
		});

		internal static Stream CreateFileStream (string path)
		{
			var factory = lazyFileCreateStreamMethod.Value;
			if (factory == null) {
				throw new PlatformNotSupportedException ();
			}

			Stream fileStream;
			try {
				fileStream = factory (path);
			} catch (ArgumentException) {
				throw;
			} catch (IOException e) {
				if (e.GetType ().Name == "DirectoryNotFoundException") {
					throw new FileNotFoundException (e.Message, path, e);
				}

				throw;
			} catch (Exception e) {
				throw new IOException (e.Message, e);
			}

			return fileStream;
		}

		private static Lazy<Action<string>> lazyFileDeleteMethod = new Lazy<Action<string>> (() => {
			Type file;
			try {
				// try contract name first:
				file = Type.GetType ("System.IO.File, System.IO.FileStream, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", throwOnError: false);
			} catch {
				file = null;
			}

			if (file == null) {
				try {
					// try corlib next:
					file = typeof(object).GetTypeInfo ().Assembly.GetType ("System.IO.File");
				} catch {
					file = null;
				}
			}

			try {
				var delete = file.GetTypeInfo ().GetDeclaredMethod ("Delete");
				return (Action<string>)delete.CreateDelegate (typeof(Action<string>));
			} catch {
				return null;
			}
		});

		internal static void Delete (string path)
		{
			var factory = lazyFileDeleteMethod.Value;
			if (factory == null) {
				throw new PlatformNotSupportedException ();
			}

			try {
				factory (path);
			} catch (ArgumentException) {
				throw;
			} catch (IOException e) {
				if (e.GetType ().Name == "DirectoryNotFoundException") {
					throw new FileNotFoundException (e.Message, path, e);
				}

				throw;
			} catch (Exception e) {
				throw new IOException (e.Message, e);
			}
		}
	}
}
