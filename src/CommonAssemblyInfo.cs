// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

#if NETCORE
[assembly: AssemblyProduct("Experimental fork of OData WebAPI for RapidAPI project")]
#else
[assembly: AssemblyProduct("Microsoft OData Web API for ASP.NET")]
#endif
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
#if !NOT_CLS_COMPLIANT
[assembly: CLSCompliant(true)]
#endif
[assembly: NeutralResourcesLanguage("en-US")]
[assembly: AssemblyMetadata("Serviceable", "True")]
