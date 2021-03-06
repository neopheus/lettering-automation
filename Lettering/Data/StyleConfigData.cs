﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lettering.Errors;

namespace Lettering.Data {
    internal class StyleConfigData {
        internal delegate string PathBuilderDelegate(OrderData order);
        internal delegate bool ExceptionCheckDelegate(OrderData order, ExceptionData exception);

        internal FilePaths filePaths;
        internal ReportType styleType;
        internal GlobalConfigData globalConfig;
        internal string fileExtension;
        internal Dictionary<int, string> pathTypes = new Dictionary<int, string>();
        internal Dictionary<string, StylePathData> paths = new Dictionary<string, StylePathData>();
        internal Dictionary<string, PathBuilderDelegate> pathBuilders = new Dictionary<string, PathBuilderDelegate>();
        internal Dictionary<string, ExportType> exports = new Dictionary<string, ExportType>();
        internal Dictionary<string, List<ExceptionData>> exceptions = new Dictionary<string, List<ExceptionData>>();
        internal Dictionary<string, ExceptionCheckDelegate> exceptionChecks = new Dictionary<string, ExceptionCheckDelegate>();

        internal StyleConfigData(ReportType styleType, GlobalConfigData globalConfig) {
            this.filePaths = new FilePaths(this);
            this.styleType = styleType;
            this.globalConfig = globalConfig;

            AddPathBuilders();
            AddExceptionChecks();
        }

        internal void AddPathBuilders() {
            pathBuilders.Add("!style", (order) => {
                //NOTE(adam): example: "TTstyle" becomes "TT STYLES\TT style
                foreach(string stylePrefix in globalConfig.stylePrefixes) {
                    OrderData tempOrder = order.Clone();
                    //NOTE(adam): at least "names" is needed here, "mirror" may not be
                    if(pathTypes[paths[tempOrder.itemCode].type] == "mirror") tempOrder.itemCode = paths[tempOrder.itemCode].mirrorStyle;
                    if(pathTypes[paths[tempOrder.itemCode].type] == "names") tempOrder.itemCode = paths[tempOrder.itemCode].mirrorStyle;

                    if(tempOrder.itemCode.StartsWith(stylePrefix)) {
                        return stylePrefix + " STYLES\\" + stylePrefix + " " + Regex.Replace(tempOrder.itemCode, stylePrefix, "");
                    }
                }
                //NOTE(adam): can insert other custom style paths here?
                ErrorHandler.HandleError(ErrorType.Log, "No style prefix found for !style path builder.");
                return "";  //NOTE(adam): ensures failure if no match
            });
            pathBuilders.Add("!size", (order) => {
                //NOTE(adam): if int size, force as int; otherwise, allow decimal part
                if(Math.Ceiling(order.size) == Math.Floor(order.size)) {
                    return (int)order.size + "INCH";
                } else {
                    return order.size + "INCH";
                }
            });
            pathBuilders.Add("!spec", (order) => { return String.Format("{0:0.#}", order.spec); });
            pathBuilders.Add("!ya", (order) => { return order.spec < 10 ? "YOUTH" : "ADULT"; });
            pathBuilders.Add("!cd", (order) => {
                //NOTE(adam): using last entered word as cheer/dance
                if(order.word4 != "") {
                    return order.word4.ToUpper();
                } else if(order.word3 != "") {
                    return order.word3.ToUpper();
                } else if(order.word2 != "") {
                    return order.word2.ToUpper();
                } else if(order.word1 != "") {
                    return order.word1.ToUpper();
                } else {
                    ErrorHandler.HandleError(ErrorType.Log, "No words for !cd path builder.");
                    return "";
                }
            });
        }

        internal void AddExceptionChecks() {
            exceptionChecks.Add("size", (order, exception) => { return order.size == exception.value; });
            exceptionChecks.Add("spec", (order, exception) => { return order.spec == exception.value; });
        }

        internal void SetFileExtension(string extension) {
            if(!(extension[0] == '.')) {
                extension = '.' + extension;
            }
            fileExtension = extension;
        }

        internal void InsertPathType(int id, string desc) {
            if(pathTypes.ContainsKey(id)) {
                ErrorHandler.HandleError(ErrorType.Log, $"Config already contains pathType.  id: {id}");
                return;
            }

            pathTypes.Add(id, desc);
        }

        internal void InsertPath(StylePathData path) {
            path.style = path.style.Replace(" ", String.Empty);

            if(paths.ContainsKey(path.style)) {
                ErrorHandler.HandleError(ErrorType.Log, $"Config already contains stylePath.  path: {path.style}");
                return;
            }

            path.wordOrder = path.wordOrder != null ? path.wordOrder : new int[] { 1, 2, 3, 4 };
            path.mirrorStyle = path.mirrorStyle.Replace(" ", String.Empty);

            paths.Add(path.style, path);
        }

        internal void InsertExport(ExportData export) {
            if(exports.ContainsKey(export.styleRegex)) {
                ErrorHandler.HandleError(ErrorType.Log, $"Config already contains export.  regex: {export.styleRegex}");
                return;
            }

            exports.Add(export.styleRegex, export.fileType);
        }

        internal void InsertException(ExceptionData exception) {
            //NOTE(adam): if there is an entry for the style, try to add to it; otherwise, create new entry
            string style = exception.style.Replace(" ", String.Empty);
            List<ExceptionData> exceptionList;
            if(exceptions.TryGetValue(style, out exceptionList)) {
                foreach(ExceptionData exl in exceptionList) {
                    if(exl.style == exception.style && exl.tag == exception.tag && exl.value == exception.value) {
                        ErrorHandler.HandleError(ErrorType.Log, $"Config already contains exception.  exception: {exception.style}:{exception.tag}={exception.value}");
                        return;
                    }
                }

                exceptionList.Add(exception);
            } else {
                exceptions.Add(style, new List<ExceptionData> { exception });
            }
        }

        internal string TryTrimStyleCode(string style) {
            style = style.Replace(" ", String.Empty);
            style = Regex.Replace(style, @"^CF", "TT");     //NOTE(adam): always treat CF as TT
            style = Regex.Replace(style, @"^JVT", "TT");    //NOTE(adam): always treat JVT as TT

            if(paths.ContainsKey(style)) return style;     //NOTE(adam): path data exists, no trimming needed

            //TODO(adam): change this to reuse trims with while & if ?
            foreach(string pattern in globalConfig.trims) {
                style = Regex.Replace(style, pattern, "");
                if(paths.ContainsKey(style)) return style;     //NOTE(adam): path data found
            }
            
            ErrorHandler.HandleError(ErrorType.Log, $"No style found in TryTrimStyleCode for final style code {style}");
            return "";
        }

        internal bool IsIgnoredStyle(OrderData order) {
            //TODO(adam): change match to not be string
            return pathTypes[paths[TryTrimStyleCode(order.itemCode)].type] == "ignore" ||
                   (paths[TryTrimStyleCode(order.itemCode)].mirrorStyle != "" &&
                    pathTypes[paths[paths[TryTrimStyleCode(order.itemCode)].mirrorStyle].type] == "ignore");
        }

        internal bool IsNameStyle(string itemCode) {
            //TODO(adam): change match to not be string
            return pathTypes[paths[TryTrimStyleCode(itemCode)].type] == "names";
        }

        internal ExportType GetExportType(string itemCode) {
            foreach(string pattern in exports.Keys) {
                if(Regex.IsMatch(TryTrimStyleCode(itemCode), pattern)) {
                    return exports[pattern];
                }
            }
            return ExportType.None;
        }

        internal ReportType GetStyleLookupType(string itemCode) {
            //TODO(adam): change match to not be string
            if(pathTypes[paths[TryTrimStyleCode(itemCode)].type] == "cut-sew_files") return ReportType.Cut;
            if(pathTypes[paths[TryTrimStyleCode(itemCode)].type] == "cut-specific") return ReportType.Cut;

            return this.styleType;
        }

        internal void SetRootPath(string rootPath) {
            filePaths.SetRootFolderPath(rootPath);
        }
    }
}
