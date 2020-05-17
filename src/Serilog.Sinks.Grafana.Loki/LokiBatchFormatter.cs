﻿// Copyright 2020 Mykhailo Shevchuk & Contributors
//
// Licensed under the MIT license;
// you may not use this file except in compliance with the License.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Grafana.Loki.Models;
using Serilog.Sinks.Grafana.Loki.Utils;
using Serilog.Sinks.Http;

namespace Serilog.Sinks.Grafana.Loki
{
    internal class LokiBatchFormatter : IBatchFormatter
    {
        private readonly IEnumerable<LokiLabel> _globalLabels;

        public LokiBatchFormatter()
        {
            _globalLabels = Enumerable.Empty<LokiLabel>();
        }

        public LokiBatchFormatter(IEnumerable<LokiLabel> globalLabels)
        {
            _globalLabels = globalLabels;
        }

        public void Format(IEnumerable<LogEvent> logEvents, ITextFormatter formatter, TextWriter output)
        {
            if (logEvents == null)
            {
                throw new ArgumentNullException(nameof(logEvents));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            var events = logEvents as LogEvent[] ?? logEvents.ToArray();

            if (events.Length == 0)
            {
                return;
            }

            var batch = new LokiBatch();

            foreach (var logEvent in events)
            {
                var stream = batch.CreateStream();
                GenerateLabels(logEvent, stream);
                GenerateEntry(logEvent, stream);
            }

            if (batch.IsNotEmpty)
            {
                output.Write(batch.Serialize());
            }
        }

        public void Format(IEnumerable<string> logEvents, TextWriter output)
        {
            throw new NotImplementedException();
        }

        private static void GenerateEntry(LogEvent logEvent, LokiStream stream)
        {
            var sb = new StringBuilder();
            sb.AppendLine(logEvent.RenderMessage());

            if (logEvent.Exception != null)
            {
                var ex = logEvent.Exception;
                while (ex != null)
                {
                    sb.AppendLine(ex.Message);
                    sb.AppendLine(ex.StackTrace);
                    ex = ex.InnerException;
                }
            }

            stream.AddEntry(logEvent.Timestamp, sb.ToString().TrimEnd('\r', '\n'));
        }

        private void GenerateLabels(LogEvent logEvent, LokiStream stream)
        {
            stream.AddLabel("level", logEvent.Level.ToGrafanaLogLevel());

            foreach (var label in _globalLabels)
            {
                stream.AddLabel(label.Key, label.Value);
            }

            foreach (var property in logEvent.Properties)
            {
                // Some enrichers generates extra quotes and it breaks the payload
                stream.AddLabel(property.Key, property.Value.ToString().Replace("\"", string.Empty));
            }
        }
    }
}