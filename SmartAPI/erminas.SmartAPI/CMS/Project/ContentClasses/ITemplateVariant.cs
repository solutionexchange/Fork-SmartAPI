// SmartAPI - .Net programmatic access to RedDot servers
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
using System.Web;
using System.Xml;
using erminas.SmartAPI.CMS.ServerManagement;
using erminas.SmartAPI.Exceptions;
using erminas.SmartAPI.Utils;

namespace erminas.SmartAPI.CMS.Project.ContentClasses
{
    //TODO templatevariant auf attributes umstellen
    public interface ITemplateVariant : IPartialRedDotObject, IProjectObject, IDeletable
    {

        new string Name { get; set; }
        /// <summary>
        ///     Assign this template to a specific project variant
        /// </summary>
        void AssignToProjectVariant(IProjectVariant variant, bool doNotPublish, bool doNotUseTidy);

        /// <summary>
        /// </summary>
        bool ContainsAreaMarksInPage { get; set; }

        IContentClass ContentClass { get; }

        /// <summary>
        ///     Copy this template over to another content class
        /// </summary>
        /// <param name="target"> </param>
        void CopyToContentClass(IContentClass target);

        /// <summary>
        ///     Timestamp of the creation of the template
        /// </summary>
        DateTime CreationDate { get; }

        /// <summary>
        ///     User who created the template
        /// </summary>
        IUser CreationUser { get; }

        /// <summary>
        ///     Content data of the template (template text). This gets changed immediately on server. No need to call commit afterwards
        /// </summary>
        string Data { get; set; }

        /// <summary>
        /// Commit changes to attriubtes to the server.
        /// If you change the template content (Data property), you don't need to call this method.
        /// </summary>
        void Commit();

        /// <summary>
        ///     Description of the template
        /// </summary>
        string Description { get; set; }

        string FileExtension { get; set;  }
        IRedDotObject Handle { get; }
        bool HasContainerPageReference { get; set; }
        bool IsLocked { get; }

        /// <summary>
        ///     Denoting whether or not a stylesheet should be automatically built into the header area of a page.
        /// </summary>
        bool IsStylesheetIncludedInHeader { get; set; }

        /// <summary>
        ///     Timestamp of the last change to the template
        /// </summary>
        DateTime LastChangeDate { get; }

        /// <summary>
        ///     User who last changed the template
        /// </summary>
        IUser LastChangeUser { get; }

        PdfOrientation PdfOrientation { get; set; }

        /// <summary>
        ///     Current release status of the template
        /// </summary>
        TemplateVariantState ReleaseStatus { get; }
    }

    /// <summary>
    ///     Represents a single template on the RedDot server
    /// </summary>
    internal class TemplateVariant : PartialRedDotProjectObject, ITemplateVariant
    {
        private DateTime _changeDate;
        private User _changeUser;
        private User _createUser;
        private DateTime _creationDate;
        private string _data;
        private string _description;
        private string _fileExtension;
        private bool _hasContainerPageReference;
        private bool _isLocked;
        private bool _isStylesheetIncluded;
        private bool _noStartEndMarkers;
        private PdfOrientation _pdfOrientation;
        private TemplateVariantState _status;

        public TemplateVariant(IContentClass contentClass, Guid guid) : base(contentClass.Project, guid)
        {
            ContentClass = contentClass;
        }

        internal TemplateVariant(IContentClass contentClass, XmlElement xmlElement)
            : base(contentClass.Project, xmlElement)
        {
            ContentClass = contentClass;
            LoadXml();
            if (IsOnlyPartiallyInitialized(xmlElement))
            {
                IsInitialized = false;
            }
        }

        /// <summary>
        ///     Assign this template to a specific project variant
        /// </summary>
        public void AssignToProjectVariant(IProjectVariant variant, bool doNotPublish, bool doNotUseTidy)
        {
            const string ASSIGN_PROJECT_VARIANT =
                @"<TEMPLATE guid=""{0}""><TEMPLATEVARIANTS> <TEMPLATEVARIANT guid=""{1}"">
                                                    <PROJECTVARIANTS action=""assign""><PROJECTVARIANT donotgenerate=""{3}"" donotusetidy=""{4}"" guid=""{2}"" />
                                                    </PROJECTVARIANTS></TEMPLATEVARIANT></TEMPLATEVARIANTS></TEMPLATE>";

            ContentClass.Project.ExecuteRQL(string.Format(ASSIGN_PROJECT_VARIANT, ContentClass.Guid.ToRQLString(),
                                                          Guid.ToRQLString(), variant.Guid.ToRQLString(),
                                                          doNotPublish.ToRQLString(), doNotUseTidy.ToRQLString()));

            ContentClass.ProjectVariantAssignments.InvalidateCache();
        }

        /// <summary>
        /// </summary>
        public bool ContainsAreaMarksInPage
        {
            get { return !LazyLoad(ref _noStartEndMarkers); }
            set { _noStartEndMarkers = !value; }
        }

        public IContentClass ContentClass { get; private set; }

        /// <summary>
        ///     Copy this template over to another content class
        /// </summary>
        /// <param name="target"> </param>
        public void CopyToContentClass(IContentClass target)
        {
            const string ADD_TEMPLATE_VARIANT = @"<TEMPLATE action=""assign"" guid=""{0}"">
                    <TEMPLATEVARIANTS action=""addnew"">
                        <TEMPLATEVARIANT name=""{1}"" description=""{2}"" code=""{3}"" fileextension=""{4}"" insertstylesheetinpage=""{5}"" nostartendmarkers=""{6}"" containerpagereference=""{7}""  pdforientation=""{8}"">{3}</TEMPLATEVARIANT></TEMPLATEVARIANTS></TEMPLATE>";
            XmlDocument xmlDoc =
                target.Project.ExecuteRQL(
                    string.Format(ADD_TEMPLATE_VARIANT, target.Guid.ToRQLString(), HttpUtility.HtmlEncode(Name),
                                  HttpUtility.HtmlEncode(Description), HttpUtility.HtmlEncode(Data),
                                  HttpUtility.HtmlEncode(FileExtension), IsStylesheetIncludedInHeader.ToRQLString(),
                                  ContainsAreaMarksInPage.ToRQLString(), HasContainerPageReference.ToRQLString(),
                                  PdfOrientation), RqlType.SessionKeyInProject);
            if (xmlDoc.DocumentElement.InnerText.Trim().Length == 0)
            {
                return;
            }
            string errorMsg = string.Format("Error during addition of template variant '{0}' to content class '{1}'.",
                                            Name, target.Name);
            //sometimes it's <IODATA><ERROR>Reason</ERROR></IODATA> and sometimes just <IODATA>ERROR</IODATA>
            XmlNodeList errorElements = xmlDoc.GetElementsByTagName("ERROR");
            if (errorElements.Count > 0)
            {
                throw new SmartAPIException(Session.ServerLogin,
                                            errorMsg + string.Format(" Reason: {0}.", errorElements[0].FirstChild.Value));
            }
            throw new SmartAPIException(Session.ServerLogin, errorMsg);
        }

        /// <summary>
        ///     Timestamp of the creation of the template
        /// </summary>
        public DateTime CreationDate
        {
            get { return LazyLoad(ref _creationDate); }
        }

        /// <summary>
        ///     User who created the template
        /// </summary>
        public IUser CreationUser
        {
            get { return LazyLoad(ref _createUser); }
        }

        /// <summary>
        ///     Content data of the template (template text)
        /// </summary>
        public string Data
        {
            get { return LazyLoad(ref _data); }
            set
            {
                const string SAVE_DATA =
                    @"<TEMPLATE action=""save"" guid=""{0}""><TEMPLATEVARIANT guid=""{1}"">{2}</TEMPLATEVARIANT></TEMPLATE>";
                XmlDocument result =
                    ContentClass.Project.ExecuteRQL(
                        String.Format(SAVE_DATA, ContentClass.Guid.ToRQLString(), Guid.ToRQLString(),
                                      HttpUtility.HtmlEncode(value)), RqlType.SessionKeyInProject);
                if (!result.DocumentElement.InnerText.Contains(ContentClass.Guid.ToRQLString()))
                {
                    var e =
                        new Exception("Could not save templatevariant '" + Name + "' for content class '" +
                                      ContentClass.Name);
                    e.Data.Add("query_result", result);
                }
                _data = value;
            }
        }

        public void Delete()
        {
            const string DELETE_TEMPLATE =
                @"<TEMPLATE><TEMPLATEVARIANTS><TEMPLATEVARIANT action=""delete"" guid=""{0}""/></TEMPLATEVARIANTS></TEMPLATE>";
            try
            {
                Project.ExecuteRQL(DELETE_TEMPLATE.RQLFormat(this), RqlType.SessionKeyInProject);
            } catch (RQLException e)
            {
                throw new SmartAPIException(Session.ServerLogin,
                                            string.Format(
                                                "Could not delete template variant {0} from content class {1}: {2}",
                                                this, ContentClass, e.Message), e);
            }
        }

        public void Commit()
        {
            const string SAVE_DATA =
                @"<TEMPLATE action=""save"" guid=""{0}""><TEMPLATEVARIANT guid=""{1}"" name=""{2}"" fileextension=""{3}"" insertstylesheetinpage=""{4}"" nostartendmarkers=""{5}"" containerpagereference=""{6}"" pdforientation=""{7}""/></TEMPLATE>";

            Project.ExecuteRQL(
                               SAVE_DATA.SecureRQLFormat(
                                                         ContentClass,
                                                         this,
                                                         Name,
                                                         FileExtension ?? RQL.SESSIONKEY_PLACEHOLDER,
                                                         IsStylesheetIncludedInHeader,
                                                         _noStartEndMarkers,
                                                         HasContainerPageReference,
                                                         PdfOrientation.ToRQLString()));


            Refresh();
        }

        /// <summary>
        ///     Description of the template
        /// </summary>
        public string Description
        {
            get { return LazyLoad(ref _description); }
            set { _description = value; }
        }

        public new string Name { get { return base.Name; } set { base.Name = value; } }

        public string FileExtension
        {
            get { return LazyLoad(ref _fileExtension); }
            set { _fileExtension = value; }
        }

        public IRedDotObject Handle
        {
            get { return new RedDotObjectHandle(Guid, Name); }
        }

        public bool HasContainerPageReference
        {
            get { return LazyLoad(ref _hasContainerPageReference); }
            set { _hasContainerPageReference = value; }
        }

        public bool IsLocked
        {
            get { return LazyLoad(ref _isLocked); }
        }

        /// <summary>
        ///     Denoting whether or not a stylesheet should be automatically built into the header area of a page.
        /// </summary>
        public bool IsStylesheetIncludedInHeader
        {
            get { return LazyLoad(ref _isStylesheetIncluded); }
            set { _isStylesheetIncluded = value; }
        }

        /// <summary>
        ///     Timestamp of the last change to the template
        /// </summary>
        public DateTime LastChangeDate
        {
            get { return LazyLoad(ref _changeDate); }
        }

        /// <summary>
        ///     User who last changed the template
        /// </summary>
        public IUser LastChangeUser
        {
            get { return LazyLoad(ref _changeUser); }
        }

        public PdfOrientation PdfOrientation
        {
            get { return LazyLoad(ref _pdfOrientation); }
            set { _pdfOrientation = value; }
        }

        /// <summary>
        ///     Current release status of the template
        /// </summary>
        public TemplateVariantState ReleaseStatus
        {
            get { return LazyLoad(ref _status); }
        }

        protected override void LoadWholeObject()
        {
            LoadXml();
        }

        protected override XmlElement RetrieveWholeObject()
        {
            const string LOAD_TEMPLATEVARIANT =
                @"<TEMPLATE><TEMPLATEVARIANT action=""load"" readonly=""1"" guid=""{0}"" /></TEMPLATE>";
            XmlDocument xmlDoc = ContentClass.Project.ExecuteRQL(string.Format(LOAD_TEMPLATEVARIANT, Guid.ToRQLString()));

            return (XmlElement) xmlDoc.GetElementsByTagName("TEMPLATEVARIANT")[0];
        }

        private static bool IsOnlyPartiallyInitialized(XmlElement xmlElement)
        {
            return xmlElement.GetAttributeNode("pdforientation") == null;
        }

        private void LoadXml()
        {
            if (!String.IsNullOrEmpty(_xmlElement.InnerText))
            {
                _data = _xmlElement.InnerText;
            }
            InitIfPresent(ref _creationDate, "createdate", XmlUtil.ToOADate);
            InitIfPresent(ref _changeDate, "changeddate", XmlUtil.ToOADate);
            InitIfPresent(ref _description, "description", x => x);
            InitIfPresent(ref _createUser, "createuserguid",
                          x =>
                          new User(ContentClass.Project.Session, Guid.Parse(x))
                              {
                                  Name = _xmlElement.GetAttributeValue("createusername")
                              });
            InitIfPresent(ref _changeUser, "changeduserguid",
                          x =>
                          new User(ContentClass.Project.Session, Guid.Parse(x))
                              {
                                  Name = _xmlElement.GetAttributeValue("changedusername")
                              });
            InitIfPresent(ref _fileExtension, "fileextension", x => x);
            InitIfPresent(ref _pdfOrientation, "pdforientation", PdfOrientationUtils.ToPdfOrientation);
            InitIfPresent(ref _isStylesheetIncluded, "insertstylesheetinpage", BoolConvert);
            InitIfPresent(ref _noStartEndMarkers, "nostartendmarkers", BoolConvert);
            InitIfPresent(ref _isLocked, "lock", BoolConvert);
            InitIfPresent(ref _hasContainerPageReference, "containerpagereference", BoolConvert);
            if (BoolConvert(_xmlElement.GetAttributeValue("draft")))
            {
                _status = TemplateVariantState.Draft;
            }
            else
            {
                _status = BoolConvert(_xmlElement.GetAttributeValue("waitforrelease"))
                              ? TemplateVariantState.WaitsForRelease
                              : TemplateVariantState.Released;
            }
        }
    }

    /// <summary>
    ///     State of the template.
    /// </summary>
    public enum TemplateVariantState
    {
        Draft,
        WaitsForRelease,
        Released
    }
}