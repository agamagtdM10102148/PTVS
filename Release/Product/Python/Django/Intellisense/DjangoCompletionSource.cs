﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.Intellisense {
    class DjangoCompletionSource : ICompletionSource {
        private readonly DjangoCompletionSourceProvider _provider;
        private readonly ITextBuffer _buffer;

        public DjangoCompletionSource(DjangoCompletionSourceProvider djangoCompletionSourceProvider, ITextBuffer textBuffer) {
            _provider = djangoCompletionSourceProvider;
            _buffer = textBuffer;
        }

        #region ICompletionSource Members

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            DjangoProject project;
            string filename = _buffer.GetFilePath();
            if (filename != null) {
                project = DjangoPackage.GetProject(filename);
                TemplateTokenKind kind;
                if (project != null &&
                    _buffer.Properties.TryGetProperty<TemplateTokenKind>(typeof(TemplateTokenKind), out kind)) {

                        if (kind == TemplateTokenKind.Block || kind == TemplateTokenKind.Variable) {
                            var compSet = new CompletionSet();

                            List<Completion> completions = GetCompletions(project, kind, _buffer.CurrentSnapshot.GetText());
                            completionSets.Add(
                                new CompletionSet(
                                    "Django Tags",
                                    "Django Tags",
                                    session.CreateTrackingSpan(_buffer),
                                    completions.ToArray(),
                                    new Completion[0]
                                )
                            );
                        }
                }
            }
        }

        const string _doubleQuotedString = @"""[^""\\]*(?:\\.[^""\\]*)*""";
        const string _singleQuotedString = @"'[^'\\]*(?:\\.[^'\\]*)*'";
        const string _numFormat = @"[-+\.]?\d[\d\.e]*";
        const string _constStr = @"
            (?:_\(" + _doubleQuotedString + @"\)|
            _\(" + _singleQuotedString + @"\)|
            " + _doubleQuotedString + @"|
            " + _singleQuotedString + @")";

        private static Regex _filterRegex = new Regex(@"
^(?<constant>" + _constStr + @")|
^(?<num>" + _numFormat + @")|
^(?<var>[\w\.]+)|
 (?:\|
     (?<filter_name>\w+)
         (?:\:
             (?:
              (?<constant_arg>" + _constStr + @")|
              (?<num_arg>" + _numFormat + @")|
              (?<var_arg>[\w\.]+)
             )
         )?
 )", RegexOptions.Compiled|RegexOptions.IgnorePatternWhitespace);

        internal static DjangoVariable ParseFilter(string filterText) {
            DjangoVariableValue filter = null;
            List<DjangoFilter> filters = new List<DjangoFilter>();
            int varStart = 0;

            foreach (Match match in _filterRegex.Matches(filterText)) {
                if (filter == null) {
                    var constantGroup = match.Groups["constant"];
                    if (constantGroup.Success) {
                        varStart = constantGroup.Index;
                        filter = new DjangoVariableValue(constantGroup.Value, DjangoVariableKind.Constant);
                    } else {
                        var varGroup = match.Groups["var"];
                        if (!varGroup.Success) {

                            var numGroup = match.Groups["num"];
                            if (!numGroup.Success) {
                                return null;
                            }
                            varStart = numGroup.Index;
                            filter = new DjangoVariableValue(numGroup.Value, DjangoVariableKind.Number);
                        } else {
                            varStart = constantGroup.Index;
                            filter = new DjangoVariableValue(varGroup.Value, DjangoVariableKind.Variable);
                        }
                    }
                } else {
                    filters.Add(GetFilterFromMatch(match));
                }
            }

            return new DjangoVariable(filter, varStart, filters.ToArray());
        }

        private static DjangoFilter GetFilterFromMatch(Match match) {
            var filterName = match.Groups["filter_name"];

            if (!filterName.Success) {
                // TODO: Report error
            }
            var filterStart = filterName.Index;

            
            DjangoVariableValue arg = null;
            int argStart = 0;
            
            var constantGroup = match.Groups["constant_arg"];
            if (constantGroup.Success) {
                arg = new DjangoVariableValue(constantGroup.Value, DjangoVariableKind.Constant);
                argStart = constantGroup.Index;
            } else {
                var varGroup = match.Groups["var_arg"];
                if (varGroup.Success) {
                    arg = new DjangoVariableValue(varGroup.Value, DjangoVariableKind.Variable);
                    argStart = varGroup.Index;
                } else {
                    var numGroup = match.Groups["num_arg"];
                    if (numGroup.Success) {
                        arg = new DjangoVariableValue(numGroup.Value, DjangoVariableKind.Number);
                        argStart = numGroup.Index;
                    }
                }
            }
            return new DjangoFilter(filterName.Value, filterStart, arg, argStart);
        }

        private List<Completion> GetCompletions(DjangoProject project, TemplateTokenKind kind, string bufferText) {
            List<Completion> completions = new List<Completion>();
            IEnumerable<string> tags;

            switch (kind) {
                case TemplateTokenKind.Block:
                    tags = project._tags.Keys;
                    break;
                case TemplateTokenKind.Variable:
                    tags = project._filters.Keys;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            string filename = _buffer.GetFilePath();
            if (filename != null) {
            }

            foreach (var tag in tags.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
                completions.Add(
                    new Completion(
                        tag,
                        tag,
                        "",
                        _provider._glyphService.GetGlyph(
                            StandardGlyphGroup.GlyphKeyword,
                            StandardGlyphItem.GlyphItemPublic
                        ),
                        "tag"
                    )
                );
            }
            return completions;
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion
    }

}
