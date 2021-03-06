﻿// SmartAPI - .Net programmatic access to RedDot servers
//  
// Copyright (C) 2013 erminas GbR
// 
// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License along with this program.
// If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using erminas.SmartAPI.CMS.Project.Folder;
using erminas.SmartAPI.CMS.Project.Publication;
using erminas.SmartAPI.Utils;
using erminas.SmartAPI.Utils.CachedCollections;

namespace erminas.SmartAPI.CMS.Project
{
    //public interface IFolders : IIndexedRDList<string, IFolder>, IProjectObject
    //{
    //    IEnumerable<IFolder> ForAssetManager();
    //}

    //internal class Folders : NameIndexedRDList<IFolder>, IFolders
    //{
    //    private readonly IProject _project;

    //    internal Folders(IProject project, Caching caching) : base(caching)
    //    {
    //        _project = project;
    //        RetrieveFunc = GetFolders;
    //    }

    //    public IEnumerable<IFolder> ForAssetManager()
    //    {
    //        return this.Where(folder => folder.IsAssetManagerFolder).ToList();
    //    }

    //    public IProject Project
    //    {
    //        get { return _project; }
    //    }

    //    public ISession Session
    //    {
    //        get { return _project.Session; }
    //    }

    //    private List<IFolder> GetFolders()
    //    {
    //        const string LIST_FILE_FOLDERS =
    //            @"<PROJECT><FOLDERS action=""list"" foldertype=""0"" withsubfolders=""1""/></PROJECT>";
    //        var xmlDoc = Project.ExecuteRQL(LIST_FILE_FOLDERS);

    //        return
    //            (from XmlElement curNode in xmlDoc.GetElementsByTagName("FOLDER")
    //             select (IFolder) new Folder(Project, curNode)).ToList();
    //    }
    //}

    public interface IFolders : IIndexedRDList<string, IFolder>, IProjectObject
    {
        IRDEnumerable<IFolder> AllIncludingSubFolders { get; }
        IRDEnumerable<IAssetManagerFolder> AssetManagerFolders { get; }
        void CreateFolder(DatabaseAssetFolderConfiguration config);
    }

    public abstract class AssetFolderConfiguration
    {
        public AssetFolderConfiguration(string name)
        {
            Name = name;
        }

        public bool AreAttributesMandatory { get; set; }
        public string Description { get; set; }
        public bool IsFolderNotAvailableInEditor { get; set; }
        public bool IsPublishingPersonalizationAttributes { get; set; }
        public int MaximumNumberOfAssetsDisplayed { get; set; }
        public string Name { get; set; }
        public IPublicationFolder PublicationFolder { get; set; }
    }

    public class DatabaseAssetFolderConfiguration : AssetFolderConfiguration
    {
        public DatabaseAssetFolderConfiguration(string name) : base(name)
        {
        }
    }

    public class FileAssetFolderConfiguration : AssetFolderConfiguration
    {
        public FileAssetFolderConfiguration(string folderName, string path) : base(folderName)
        {
            Path = path;
        }

        public bool IsTransmittingCredentials { get; set; }
        public bool IsVersioningActive { get; set; }
        public string Path { get; set; }

        public string UserName { get; set; }
        public string UserPassword { get; set; }
    }

    internal class Folders : NameIndexedRDList<IFolder>, IFolders
    {
        private readonly IProject _project;

        internal Folders(IProject project, Caching caching) : base(caching)
        {
            _project = project;
            RetrieveFunc = GetFolders;
        }

        public IRDEnumerable<IFolder> AllIncludingSubFolders
        {
            get
            {
                return
                    this.Union(
                        this.Where(folder => folder is IAssetManagerFolder)
                            .Cast<IAssetManagerFolder>()
                            .SelectMany(folder => folder.SubFolders)).ToRDEnumerable();
            }
        }

        public IRDEnumerable<IAssetManagerFolder> AssetManagerFolders
        {
            get { return this.Where(folder => folder is IAssetManagerFolder).Cast<IAssetManagerFolder>().ToRDEnumerable(); }
        }

        public void CreateFolder(DatabaseAssetFolderConfiguration config)
        {
            const string CREATE_FOLDER =
                @"<PROJECT><FOLDER shared=""0"" name=""{0}"" description=""{1}"" foldertype=""0"" catalog=""1"" savetype=""0"" action=""addnew"" webfolder=""{2}"" exportfolder="""" obligatoryattributes=""{3}"" personalization=""{4}"" maxlistcount=""{5}"" hideintexteditor=""{6}""></FOLDER></PROJECT>";

            Project.ExecuteRQL(CREATE_FOLDER.SecureRQLFormat(config.Name, config.Description, config.PublicationFolder,
                                                             config.AreAttributesMandatory,
                                                             config.IsPublishingPersonalizationAttributes,
                                                             config.MaximumNumberOfAssetsDisplayed,
                                                             config.IsFolderNotAvailableInEditor));
        }

        public IFolder GetByGuidIncludingSubFolders(Guid folderGuid)
        {
            return AllIncludingSubFolders.First(folder => folder.Guid == folderGuid);
        }

        public IProject Project
        {
            get { return _project; }
        }

        public ISession Session
        {
            get { return _project.Session; }
        }

        public bool TryGetByGuidIncludingSubFolders(Guid folderGuid, out IFolder folder)
        {
            folder = AllIncludingSubFolders.FirstOrDefault(folder2 => folder2.Guid == folderGuid);
            return folder != null;
        }

        private List<IFolder> GetFolders()
        {
            const string LIST_FILE_FOLDERS = @"<PROJECT><FOLDERS action=""list"" withsubfolders=""0""/></PROJECT>";
            var xmlDoc = Project.ExecuteRQL(LIST_FILE_FOLDERS);

            return (from XmlElement curNode in xmlDoc.GetElementsByTagName("FOLDER")
                    where InternalFolderFactory.HasSupportedFolderType(curNode)
                    select InternalFolderFactory.CreateFolder(_project, curNode)).ToList();
        }
    }
}