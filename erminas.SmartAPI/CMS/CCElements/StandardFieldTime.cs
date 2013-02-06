﻿// Smart API - .Net programmatic access to RedDot servers
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

using System.Xml;
using erminas.SmartAPI.CMS.CCElements.Attributes;

namespace erminas.SmartAPI.CMS.CCElements
{
    public class StandardFieldTime : StandardFieldNonDate
    {
        public StandardFieldTime(ContentClass contentClass, XmlElement xmlElement) : base(contentClass, xmlElement)
        {
            CreateAttributes("eltlcid", "eltformatting", "eltformatno");
        }

        public bool IsUserDefinedTimeFormat
        {
            get { return ((DateTimeFormatAttribute) GetAttribute("eltformatno")).Value == null; }
        }

        public Locale Locale
        {
            get { return ((LocaleXmlNodeAttribute) GetAttribute("eltlcid")).Value; }
            set { ((LocaleXmlNodeAttribute) GetAttribute("eltlcid")).Value = value; }
        }

        public DateTimeFormat TimeFormat
        {
            get
            {
                return ((DateTimeFormatAttribute) GetAttribute("eltformatno")).Value ??
                       DateTimeFormat.USER_DEFINED_TIME_FORMAT;
            }
            set { ((DateTimeFormatAttribute) GetAttribute("eltformatno")).Value = value; }
        }

        public string UserDefinedTimeFormat
        {
            get { return ((StringXmlNodeAttribute) GetAttribute("eltformatting")).Value; }
            set { ((StringXmlNodeAttribute) GetAttribute("eltformatting")).Value = value; }
        }
    }
}