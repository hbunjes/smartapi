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
using erminas.SmartAPI.CMS.Project;
using erminas.SmartAPI.Exceptions;
using erminas.SmartAPI.Utils;
using erminas.SmartAPI.Utils.CachedCollections;

namespace erminas.SmartAPI.CMS.ServerManagement
{
    [Flags]
    public enum UserPofileChangeRestrictions
    {
        NameAndDescription = 1,
        EMailAdress = 2,
        ConnectedDirectoryService = 4,
        Password = 8,
        InterfaceLanguageAndLocale = 16,
        SmartEditNavigation = 32,
        PreferredTextEditor = 64,
        DirectEdit = 128
    }

    public enum DirectEditActivation
    {
        CtrlAndMouseButton = 0,
        MouseButtonOnly = 1
    }

    public interface IUserProjectGroupAssignment : ISessionObject
    {
        IProject Project { get; }
        IList<IGroup> Groups { get; }

        IUser User { get; }
    }

    public interface IUserProjectGroups : IIndexedCachedList<string, IUserProjectGroupAssignment>, ISessionObject
    {
        
    }

    public class UserProjectGroups : IndexedCachedList<string, IUserProjectGroupAssignment>, IUserProjectGroups
    {
        private IUser _user;

        internal UserProjectGroups(IUser user, Caching caching) : base(assignment => assignment.Project.Name, caching)
        {
            _user = user;
            Session = _user.Session;
            RetrieveFunc = GetProjectGroupAssignments;
        }

        public ISession Session { get; private set; }

        private List<IUserProjectGroupAssignment> GetProjectGroupAssignments()
        {
            const string LIST_USER_PROJECTS = @"<ADMINISTRATION><USER guid=""{0}""><PROJECTS action=""listgroups"" /></USER></ADMINISTRATION>";

            var xmlDoc = Session.ExecuteRQL(LIST_USER_PROJECTS.RQLFormat(User));
            var projectEntries = xmlDoc.GetElementsByTagName("PROJECT");
            return (from XmlElement assignmentElement in xmlDoc.GetElementsByTagName("PROJECT")
                    select (IUserProjectGroupAssignment) new UserProjectGroupAssignment(_user, Session.ServerManager.Projects.GetByGuid(assignmentElement.GetGuid()), GetGroups(assignmentElement))).ToList();
        }

        private IList<IGroup> GetGroups(XmlElement assignmentElement)
        {
            return (from XmlElement groupElement in assignmentElement.GetElementsByTagName("GROUP") where groupElement.GetBoolAttributeValue("checked").GetValueOrDefault(true)
              select (IGroup) new Group(Session, groupElement.GetGuid())).ToList();
        }

        public IUser User { get { return _user;} }
    }

    public class UserProjectGroupAssignment : IUserProjectGroupAssignment 
    {
        private readonly ISession _session;
        private readonly IUser _user;
        private readonly IList<IGroup> _groups;
        private readonly IProject _project;
        public UserProjectGroupAssignment(IUser user, IProject project, IList<IGroup> groups)
        {
            _project = project;
            _user = user;
            _session = _user.Session;
            _groups = groups;
        }

        public ISession Session { get { return _session; } }
        public IProject Project { get { return _project; } }
        public IList<IGroup> Groups { get { return _groups; }}

        public IUser User { get { return _user; } }
    }

    public interface IUser : IPartialRedDotObject, ISessionObject, IDeletable
    {
        Guid AccountSystemGuid { get; }
        void Commit();
        string Description { get; set; }
        DirectEditActivation DirectEditActivationType { get; set; }
        string EMail { get; set; }
        string FullName { get; set; }
        int Id { get; }
        bool IsAlwaysScrollingOpenTreeSegmentsInTheVisibleArea { get; set; }
        bool IsPasswordwordChangeableByCurrentUser { get; }
        IDialogLocale LanguageOfUserInterface { get; set; }
        DateTime LastLoginDate { get; }
        ISystemLocale Locale { get; set; }
        int MaxLevel { get; }
        int MaximumNumberOfSessions { get; set; }
        IUserModuleAssignment ModuleAssignment { get; }
        new string Name { get; set; }
        string NavigationType { get; }
        string Password { set; }
        int PreferredEditor { get; }

        /// <summary>
        /// Indicates if the user is not (no longer) available in the system.
        /// For example the content class history might still contain a user which was deleted later on.
        /// In this case IsUnknownUser will be true, but you can still access its guid.
        /// </summary>
        bool IsUnknownUser { get; }

        IUserProjects Projects { get; }

        IUserProjectGroups ProjectGroups { get; }

        UserPofileChangeRestrictions UserPofileChangeRestrictions { get; set; }
    }

    /// <summary>
    ///     A user in the RedDot system.
    /// </summary>
    internal class User : PartialRedDotObject, IUser
    {
        private Guid _accountSystemGuid;
        private string _description;
        private string _email;
        private string _fullname;
        private int _id;
        private DirectEditActivation _invertDirectEdit;
        private bool _isPasswordChangeableByCurrentUser;
        private bool _isScrolling;
        private ISystemLocale _locale;
        private DateTime _loginDate;
        private int _maxLevel;
        private int _maxSessionCount;
        private string _navigationType;
        private string _password;
        private int _preferredEditor;
        private string _preferredEditorString;
        private IDialogLocale _userInterfaceLanguage;
        private UserPofileChangeRestrictions _userPofileChangeRestrictions;
        private bool _isUnknownUser;

        public User(ISession session, Guid userGuid) : base(session, userGuid)
        {
            Init();
        }

        /// <summary>
        ///     Reads user data from XML-Element "USER" like: <pre>...</pre>
        /// </summary>
        /// <exception cref="FileDataException">Thrown if element doesn't contain valid data.</exception>
        /// <param name="session"> The cms session used to retrieve this user </param>
        /// <param name="xmlElement"> USER XML-Element to get data from </param>
        internal User(ISession session, XmlElement xmlElement) : base(session, xmlElement)
        {
            Init();

            LoadXml();
        }

        public Guid AccountSystemGuid
        {
            get { return LazyLoad(ref _accountSystemGuid); }
        }

        public void Commit()
        {
            const string SAVE_USER =
                @"<ADMINISTRATION><USER action=""save"" guid=""{0}"" name=""{1}"" fullname=""{2}"" description=""{3}"" email=""{4}"" userlanguage=""{5}"" maxlogin=""{6}"" invertdirectedit=""{7}"" treeautoscroll=""{8}"" preferrededitor=""{9}"" navigationtype=""{10}"" lcid=""{11}"" userlimits=""{12}"" {13}/></ADMINISTRATION>";

            var passwordAttribute = _password != null ? "password=\"" + _password + '"' : "";
            var query = SAVE_USER.SecureRQLFormat(this, Name, FullName, Description, EMail,
                                                  LanguageOfUserInterface.LanguageAbbreviation, MaximumNumberOfSessions,
                                                  (int) DirectEditActivationType,
                                                  IsAlwaysScrollingOpenTreeSegmentsInTheVisibleArea, PreferredEditor,
                                                  NavigationType, Locale, (int) UserPofileChangeRestrictions,
                                                  passwordAttribute);
            Session.ExecuteRQL(query, RQL.IODataFormat.LogonGuidOnly);
        }

        public void Delete()
        {
            const string DELETE_USER = @"<ADMINISTRATION><USER action=""delete"" guid=""{0}"" /></ADMINISTRATION>";
            var xmlDoc = Session.ExecuteRQL(DELETE_USER.RQLFormat(this), RQL.IODataFormat.LogonGuidOnly);
            if (!xmlDoc.IsContainingOk())
            {
                throw new SmartAPIException(Session.ServerLogin, string.Format("Could not delete user {0}", this));
            }
            Session.ServerManager.Users.InvalidateCache();
        }

        public string Description
        {
            get { return LazyLoad(ref _description); }
            set { _description = value; }
        }

        public DirectEditActivation DirectEditActivationType
        {
            get { return _invertDirectEdit; }
            set
            {
                EnsureInitialization();
                _invertDirectEdit = value;
            }
        }

        public string EMail
        {
            get { return LazyLoad(ref _email); }
            set
            {
                EnsureInitialization();
                _email = value;
            }
        }

        public string FullName
        {
            get { return LazyLoad(ref _fullname); }
            set
            {
                EnsureInitialization();
                _fullname = value;
            }
        }

        public int Id
        {
            get { return LazyLoad(ref _id); }
        }

        public bool IsAlwaysScrollingOpenTreeSegmentsInTheVisibleArea
        {
            get { return LazyLoad(ref _isScrolling); }
            set
            {
                EnsureInitialization();
                _isScrolling = value;
            }
        }

        public bool IsPasswordwordChangeableByCurrentUser
        {
            get { return LazyLoad(ref _isPasswordChangeableByCurrentUser); }
        }

        public IDialogLocale LanguageOfUserInterface
        {
            get { return LazyLoad(ref _userInterfaceLanguage); }
            set
            {
                EnsureInitialization();
                _userInterfaceLanguage = value;
            }
        }

        public DateTime LastLoginDate
        {
            get { return LazyLoad(ref _loginDate); }
        }

        public ISystemLocale Locale
        {
            get { return LazyLoad(ref _locale); }
            set
            {
                EnsureInitialization();
                _locale = value;
            }
        }

        public int MaxLevel
        {
            get { return LazyLoad(ref _maxLevel); }
        }

        public int MaximumNumberOfSessions
        {
            get { return LazyLoad(ref _maxSessionCount); }
            set
            {
                EnsureInitialization();
                _maxSessionCount = value;
            }
        }

        public IUserModuleAssignment ModuleAssignment { get; private set; }

        public new string Name
        {
            get { return base.Name; }
            set { _name = value; }
        }

        public string NavigationType
        {
            get { return LazyLoad(ref _navigationType); }
        }

        public string Password
        {
            set { _password = value; }
        }

        [Obsolete("In newer version the preferred editor can be a string. In this case this is set to -1. Please use PreferredEditorString to always get a correct value.")]
        public int PreferredEditor
        {
            get { return LazyLoad(ref _preferredEditor); }
        }

        public string PreferredEditorString
        {
            get { return LazyLoad(ref _preferredEditorString); }
        }

        public bool IsUnknownUser { get{
           EnsureInitialization();
            return _isUnknownUser;
        } private set { _isUnknownUser = value; } }

        public IUserProjects Projects { get; private set; }
        public IUserProjectGroups ProjectGroups { get; private set; }

        public UserPofileChangeRestrictions UserPofileChangeRestrictions
        {
            get { return LazyLoad(ref _userPofileChangeRestrictions); }
            set
            {
                EnsureInitialization();
                _userPofileChangeRestrictions = value;
            }
        }

        protected override void LoadWholeObject()
        {
            LoadXml();
        }

        protected override XmlElement RetrieveWholeObject()
        {
            var xmlDocument = new XmlDocument();
            try
            {
                const string LOAD_USER = @"<ADMINISTRATION><USER action=""load"" guid=""{0}""/></ADMINISTRATION>";
                string answer = Session.ExecuteRQLRaw(String.Format(LOAD_USER, Guid.ToRQLString()),
                    RQL.IODataFormat.LogonGuidOnly);
                xmlDocument.LoadXml(answer);

                return (XmlElement) xmlDocument.GetElementsByTagName("USER")[0];
            }
            catch ( RQLException e )
            {
                IsUnknownUser = true;
                return xmlDocument.CreateElement("USER");
            }
        }

        private void Init()
        {
            Projects = new UserProjects(this, Caching.Enabled);
            ModuleAssignment = new UserModuleAssignment(this);
            ProjectGroups = new UserProjectGroups(this, Caching.Enabled);
        }

        private void LoadXml()
        {
            InitIfPresent(ref _id, "id", int.Parse);
            InitIfPresent(ref _maxLevel, "maxlevel", int.Parse);
            InitIfPresent(ref _maxSessionCount, "maxlogin", int.Parse);
            try
            {
                InitIfPresent(ref _preferredEditor, "preferrededitor", int.Parse);
            }
            catch
            {
                // Preferrededitor can now also be a string. In this case set the int to -1.
                _preferredEditor = -1;
            }
            _preferredEditorString = _xmlElement.GetAttributeValue("preferrededitor");
            _fullname = _xmlElement.GetAttributeValue("fullname");
            _description = _xmlElement.GetAttributeValue("description");
            _email = _xmlElement.GetAttributeValue("email");
            _xmlElement.TryGetGuid("accountsystemguid", out _accountSystemGuid);
            InitIfPresent(ref _userInterfaceLanguage, "userlanguage", Session.DialogLocales.Get);
            InitIfPresent(ref _locale, "lcid", s => Session.Locales[int.Parse(s)]);
            InitIfPresent(ref _isScrolling, "treeautoscroll", BoolConvert);
            _loginDate = _xmlElement.GetOADate("logindate").GetValueOrDefault();
            InitIfPresent(ref _invertDirectEdit, "invertdirectedit", StringConversion.ToEnum<DirectEditActivation>);
            InitIfPresent(ref _isPasswordChangeableByCurrentUser, "disablepassword", x => !BoolConvert(x));
            InitIfPresent(ref _userPofileChangeRestrictions, "userlimits",
                          StringConversion.ToEnum<UserPofileChangeRestrictions>);
        }
    }
}