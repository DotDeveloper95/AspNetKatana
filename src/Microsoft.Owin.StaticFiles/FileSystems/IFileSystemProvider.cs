﻿// <copyright file="IFileSystemProvider.cs" company="Katana contributors">
//   Copyright 2011-2013 Katana contributors
// </copyright>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Microsoft.Owin.StaticFiles.FileSystems
{
    /// <summary>
    /// A file system abstraction
    /// </summary>
    public interface IFileSystemProvider
    {
        /// <summary>
        /// Locate a file at the given path, if any
        /// </summary>
        /// <param name="subpath">The path that identifies the file</param>
        /// <param name="fileInfo">The discovered file, if any</param>
        /// <returns>True if a file was located at the given path</returns>
        bool TryGetFileInfo(string subpath, out IFileInfo fileInfo);

        /// <summary>
        /// Locate a directory at the given path, if any
        /// </summary>
        /// <param name="subpath">The path that identifies the file</param>
        /// <param name="directoryInfo">The discovered directory, if any</param>
        /// <returns>True if a directory was located at the given path</returns>
        bool TryGetDirectoryInfo(string subpath, out IDirectoryInfo directoryInfo);
    }
}
