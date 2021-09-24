﻿// <copyright file="MySqlActivitySourceHelper.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.MySqlData
{
    /// <summary>
    /// Helper class to hold common properties used by MySqlDataDiagnosticListener.
    /// </summary>
    internal class MySqlActivitySourceHelper
    {
        public const string MysqlDatabaseSystemName = "mysql";

        public static readonly AssemblyName AssemblyName = typeof(MySqlActivitySourceHelper).Assembly.GetName();
        public static readonly string ActivitySourceName = AssemblyName.Name;
        public static readonly string ActivityName = ActivitySourceName + ".Execute";

        public static readonly IEnumerable<KeyValuePair<string, object>> CreationTags = new[]
        {
            new KeyValuePair<string, object>(SemanticConventions.AttributeDbSystem, MysqlDatabaseSystemName),
        };

        private static readonly Version Version = typeof(MySqlActivitySourceHelper).Assembly.GetName().Version;
#pragma warning disable SA1202 // Elements should be ordered by access <- In this case, Version MUST come before ActivitySource otherwise null ref exception is thrown.
        internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
#pragma warning restore SA1202 // Elements should be ordered by access
    }
}
