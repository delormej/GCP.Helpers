// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GcpHelpers.Logging.Console
{
    public static class GoogleCloudConsoleFormatterConfig
    {
        public static void AddGoogleFormatLogger(this ILoggingBuilder logging)
        {
            logging
                .AddConsoleFormatter<GoogleCloudConsoleFormatter, GoogleCloudConsoleFormatterOptions>(
                    options => options.IncludeScopes = true)
                .AddConsole(options => 
                    options.FormatterName = nameof(GoogleCloudConsoleFormatter));
        }
    }

    /// <summary>
    /// Options for <see cref="GoogleCloudConsoleFormatter"/>.
    /// </summary>
    public class GoogleCloudConsoleFormatterOptions : ConsoleFormatterOptions
    {
        // TODO: Service context, labels etc
    }
}