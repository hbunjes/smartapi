using System;
using System.Xml;
using erminas.SmartAPI.Exceptions;
using erminas.SmartAPI.Utils;

namespace erminas.SmartAPI.CMS
{
    [Flags]
    public enum ExtendedUserRole
    {
        TemplateEditor = 1,
        TranslationEditor = 2
    }

    public enum UserRole
    {
        Administrator = 1,
        SiteBuilder = 2,
        Editor = 3,
        Author = 4,
        Visitor = 5
    }

    public class UserProjectAssignment
    {
        private readonly User _user;

        internal UserProjectAssignment(User user, XmlElement projectAssignment)
        {
            _user = user;
            LoadXml(projectAssignment);
        }

        private UserProjectAssignment(User user, Project project, UserRole role, ExtendedUserRole extendedUserRole)
        {
            Project = project;
            _user = user;
            UserRole = role;
            IsTemplateEditor = extendedUserRole.HasFlag(ExtendedUserRole.TemplateEditor);
            IsTranslationEditor = extendedUserRole.HasFlag(ExtendedUserRole.TranslationEditor);
        }

        public UserRole UserRole { get; set; }

        public User User
        {
            get { return _user; }
        }

        public Project Project { get; private set; }

        public bool IsTemplateEditor { get; set; }

        public bool IsTranslationEditor { get; set; }

        internal static UserProjectAssignment Create(User user, Project project, UserRole role,
                                                     ExtendedUserRole extendedUserRole)
        {
            var assignment = new UserProjectAssignment(user, project, role, extendedUserRole);
            assignment.Commit();

            return assignment;
        }

        public void Commit()
        {
            //TODO check results
            const string SAVE_USER_RIGHTS =
                @"<ADMINISTRATION><USER action=""save"" guid=""{0}""><PROJECTS><PROJECT guid=""{1}"" checked=""1"" lm=""{2}"" te=""{3}"" userlevel=""{4}""/></PROJECTS></USER></ADMINISTRATION>";

            User.Session.ExecuteRQL(SAVE_USER_RIGHTS.RQLFormat(User, Project, IsTranslationEditor, IsTemplateEditor,
                                                               (int) UserRole));
        }

        public void Delete()
        {
            User.UnassignProject(Project);
        }

        private void LoadXml(XmlElement projectAssignment)
        {
            Project = new Project(_user.Session, projectAssignment.GetGuid()) {Name = projectAssignment.GetName()};

            UserRole = (UserRole) projectAssignment.GetIntAttributeValue("userlevel").GetValueOrDefault();
            IsTemplateEditor = HasRight(projectAssignment, "templateeditorright");
            IsTranslationEditor = HasRight(projectAssignment, "languagemanagerright");
        }

        private static bool HasRight(XmlElement projectElement, string attributeName)
        {
            var intAttributeValue = projectElement.GetIntAttributeValue(attributeName);
            if (intAttributeValue == null)
            {
                throw new SmartAPIException(string.Format("Missing attribute '{0}' in user/project assignment",
                                                          attributeName));
            }

            return intAttributeValue.Value == -1;
        }
    }
}