﻿using Microsoft.Extensions.Logging;
using Refit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.HttpStatusCode;
using static System.Net.Sockets.SocketError;
using static System.Net.WebExceptionStatus;
using static XamarinFiles.FancyLogger.Characters;
using static XamarinFiles.PdHelpers.Shared.StatusCodeDetails;

namespace XamarinFiles.FancyLogger
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class FancyLogger : IFancyLogger
    {
        #region Fields - Services

        private readonly ILogger _logger;

        private readonly Serializer _serializer;

        #endregion

        #region Fields - Options

        public static readonly FancyLoggerOptions
            DefaultLoggerOptions = new();

        public static readonly JsonSerializerOptions
            DefaultReadOptions = new()
            {
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true
            };

        public static readonly JsonSerializerOptions
            DefaultWriteJsonOptions = new()
            {
                WriteIndented = true
            };

        #endregion

        #region Constructor

        // TODO Test with other log destinations after finish porting test code
        public FancyLogger(ILogger logger = null,
            FancyLoggerOptions loggerOptions = null,
            JsonSerializerOptions readJsonOptions = null,
            JsonSerializerOptions writeJsonOptions = null)
        {
            LoggerOptions = loggerOptions ?? DefaultLoggerOptions;
            ReadJsonOptions = readJsonOptions ?? DefaultReadOptions;
            WriteJsonOptions = writeJsonOptions ?? DefaultWriteJsonOptions;

            AllLinesPrefixString = loggerOptions!.AllLines.PrefixString;
            AllLinesPadLength = loggerOptions.AllLines.PadLength;
            AllLinesPadString = loggerOptions.AllLines.PadString;

            LongDividerLinesPadLength = loggerOptions.LongDividerLines.PadLength;
            LongDividerLinesPadString = loggerOptions.LongDividerLines.PadString;

            ShortDividerLinesPadLength = loggerOptions.ShortDividerLines.PadLength;
            ShortDividerLinesPadString = loggerOptions.ShortDividerLines.PadString;

            SectionLinesPadLength = loggerOptions.SectionLines.PadLength;
            SectionLinesPadString = loggerOptions.SectionLines.PadString;

            SubsectionLinesPadLength = loggerOptions.SubsectionLines.PadLength;
            SubsectionLinesPadString = loggerOptions.SubsectionLines.PadString;

            HeaderLinesPadLength = loggerOptions.HeaderLines.PadLength;
            HeaderLinesPadString = loggerOptions.HeaderLines.PadString;

            FooterLinesPadLength = loggerOptions.FooterLines.PadLength;
            FooterLinesPadString = loggerOptions.FooterLines.PadString;

            _logger = logger ?? LoggerCreator.CreateLogger(AllLinesPrefixString);
            _serializer = new Serializer(ReadJsonOptions, WriteJsonOptions);
        }

        #endregion

        #region Public Properties

        // Constructor Parameters

        public FancyLoggerOptions LoggerOptions { get; }

        public JsonSerializerOptions ReadJsonOptions { get; }

        public JsonSerializerOptions WriteJsonOptions { get; }

        // FancyLoggerOptions Values

        public string AllLinesPrefixString { get; }
        public int AllLinesPadLength { get; }
        public string AllLinesPadString { get; }

        public int LongDividerLinesPadLength { get; }
        public string LongDividerLinesPadString { get; }

        public int ShortDividerLinesPadLength { get; }
        public string ShortDividerLinesPadString { get; }

        public int SectionLinesPadLength { get; }
        public string SectionLinesPadString { get; }

        public int SubsectionLinesPadLength { get; }
        public string SubsectionLinesPadString { get; }

        public int HeaderLinesPadLength { get; }
        public string HeaderLinesPadString { get; }

        public int FooterLinesPadLength { get; }
        public string FooterLinesPadString { get; }

        #endregion

        #region Public Methods - Exceptions

        // Exception Router

        // TODO Add toggle for LogError vs LogWarning
        public void LogException(Exception exception)
        {
            switch (exception)
            {
                case ValidationApiException validationException:
                    LogApiException(validationException);

                    break;
                case ApiException apiException:
                    LogApiException(apiException);

                    break;
                case HttpRequestException httpRequestException:
                    LogHttpRequestException(httpRequestException);

                    break;
                case JsonException jsonException:
                    LogJsonException(jsonException);

                    break;
                default:
                    LogGeneralException(exception);

                    break;
            }
        }

        // Direct Access

        public void LogApiException(ApiException apiException)
        {
            var request = apiException.RequestMessage;

            LogCommonException(apiException, "API EXCEPTION");

            LogWarning(
                $"OPERATION:  {request.Method} - {apiException.Uri}" + NewLine);

            if (string.IsNullOrWhiteSpace(apiException.Content))
                return;

            // TODO Document expectation of ProblemDetails or handle alternative
            LogObject<ProblemDetails>(apiException.Content);
        }

        public void LogCommonException(Exception exception, string outerLabel,
            string innerLabel = "INNER EXCEPTION")
        {
            LogWarning($"{outerLabel}:  {exception.Message}{NewLine}");

            var innerExceptionMessage = exception.InnerException?.Message;

            if (string.IsNullOrWhiteSpace(innerExceptionMessage))
                return;

            LogWarning($"{Indent}{innerLabel}:  {innerExceptionMessage}{NewLine}");
        }

        public void LogGeneralException(Exception exception)
        {
            LogCommonException(exception, "EXCEPTION");
        }

        [SuppressMessage("ReSharper", "MergeIntoPattern")]
        public void LogHttpRequestException(HttpRequestException requestException)
        {
            const string outerExceptionLabel = "HTTP REQUEST EXCEPTION";
            // TODO Use when > .NET Standard 2.0 for Xamarin.Forms
            //var outerStatusCode = requestException.StatusCode;

            string innerExceptionLabel;
            HttpStatusCode? innerStatusCode = null;

            // TODO Add more networking error conditions
            switch (requestException.InnerException)
            {
                case SocketException socketException
                    when socketException.SocketErrorCode == ConnectionRefused:

                    innerExceptionLabel =
                        "SOCKET EXCEPTION - ConnectionRefused";

                    innerStatusCode = ServiceUnavailable;

                    break;
                case WebException webException
                    when webException.Status == NameResolutionFailure:

                    innerExceptionLabel =
                        "WEB EXCEPTION - NameResolutionFailure";

                    innerStatusCode = ServiceUnavailable;

                    break;

                default:
                    innerExceptionLabel = "INNER EXCEPTION";

                    break;
            }

            innerExceptionLabel += $"{innerStatusCode?.ToString() ?? ""}";

            LogCommonException(requestException, outerExceptionLabel,
                innerExceptionLabel);
        }

        public void LogJsonException(JsonException jsonException)
        {
            LogCommonException(jsonException, "JSON EXCEPTION");

            LogWarning($"LINE:  {jsonException.LineNumber}"
                       + $"{Indent}-{Indent}{jsonException.BytePositionInLine}");
            LogWarning($"PATH:  {jsonException.Path}");
        }

        #endregion

        #region Public Methods - General Logging

        public void LogDebug(string format, bool addIndent = false,
            bool newLineAfter = false, params object[] args)
        {
            try
            {
                var messagePrefix =
                    // Difference in prefix length: "Debug" vs "Information"
                    new string(' ', 6) +
                    AddIndent(addIndent);

                var message = messagePrefix
                    // TODO Handle format + args with separate {}s like Dictionary
                    + (args is null || args.Length == 0
                        ? format
                        : string.Format(format, args));

                if (newLineAfter)
                    message += NewLine;

                _logger.LogDebug("{message}", message);

            }
            catch (Exception exception)
            {
                LogException(exception);
            }
        }

        public void LogError(string format, params object[] args)
        {
            var message = string.Format(format + NewLine, args);

            _logger.LogError("{message}", message);
        }

        public void LogInfo(string format, bool addIndent = false,
            bool newLineAfter = true, params object[] args)
        {
            var message =
                AddIndent(addIndent) + string.Format(format, args);
            if (newLineAfter)
                message += NewLine;

            _logger.LogInformation("{message}", message);
        }

        public void LogObject<T>(object obj, bool ignore = false,
            bool keepNulls = false, string label = null,
            bool newLineAfter = true)
        {
            if (ignore || obj is null)
                return;

            try
            {
                var (formattedJson, problemDetails) =
                    _serializer.ToJson<T>(obj, keepNulls);

                if (problemDetails != null)
                    LogProblemDetails(problemDetails);

                if (string.IsNullOrWhiteSpace(formattedJson))
                    return;

                LogTrace($"{label}:" + NewLine + formattedJson,
                    newLineAfter: newLineAfter);
            }
            catch (Exception exception)
            {
                LogException(exception);
            }
        }

        public void LogScalar(string label, string value,
            bool addIndent = false, bool newLineAfter = true)
        {
            var message =
                // Difference in prefix length: "Trace" vs "Information"
                new string(' ', 6) +
                AddIndent(addIndent) + $"{label}{Indent}={Indent}{value}";
            if (newLineAfter)
                message += NewLine;

            _logger.LogTrace("{message}", message);
        }

        public void LogTrace(string format, bool addIndent = false,
            bool newLineAfter = false, params object[] args)
        {
            var messagePrefix =
                // Difference in prefix length: "Trace" vs "Information"
                new string(' ', 6) +
                AddIndent(addIndent);

            var message = messagePrefix
                // TODO Handle format + args with separate {}s like Dictionary
                + (args is null || args.Length == 0
                    ? format
                    : string.Format(format, args));

            if (newLineAfter)
                message += NewLine;

            _logger.LogTrace("{message}", message);
        }

        public void LogWarning(string format, bool addIndent = false,
            bool newLineAfter = true, params object[] args)
        {
            var message =
                // Difference in prefix length: "Warning" vs "Information"
                new string(' ', 5) +
                AddIndent(addIndent) + string.Format(format, args);
            if (newLineAfter)
                message += NewLine;

            _logger.LogWarning("{message}", message);
        }

        #endregion

        #region Public Methods - Specialized Logging

        // TODO Add toggle for LogError vs LogWarning
        public void LogProblemDetails(ProblemDetails problemDetails)
        {
            if (problemDetails == null)
                return;

            try
            {
                LogWarning($"PROBLEMDETAILS", newLineAfter:false);
                LogWarning($"Title: '{problemDetails.Title}'",
                    newLineAfter:false);
                LogWarning($"Detail: '{problemDetails.Detail}'",
                    newLineAfter:false);

                LogDebug($"Status Code: {Indent}{problemDetails.Status}"
                    + $" - {HttpStatusDetails[problemDetails.Status].Title}");
                LogDebug($"Instance URL: {Indent}'{problemDetails.Instance}'");
                LogDebug($"Type Info: {Indent}'{problemDetails.Type}'");

                LogObject<Dictionary<string, string[]>>(
                    problemDetails.Errors, label: "Errors Dictionary",
                    newLineAfter: false);
                LogObject<Dictionary<string, object>>(
                    problemDetails.Extensions, label: "Extensions Dictionary",
                    newLineAfter: true);
            }
            catch (Exception exception)
            {
                LogException(exception);
            }
        }

        #endregion

        #region Public Methods - Structral Logging

        // Horizontal Lines

        public void LogLongDividerLine()
        {
            var dividerLine =
                "".PadRight(LongDividerLinesPadLength, LongDividerLinesPadString);

            LogInfo(dividerLine, newLineAfter: true);
        }

        public void LogShortDividerLine()
        {
            var dividerLine =
                "".PadRight(ShortDividerLinesPadLength, ShortDividerLinesPadString);

            LogInfo(dividerLine, newLineAfter: true);
        }

        // Section and Subsection

        public void LogSection(string format, params object[] args)
        {
            var message = string.Format(format, args).Trim() + " ";
            var paddedMessage =
                message.PadRight(SectionLinesPadLength, SectionLinesPadString);

            LogInfo(paddedMessage, newLineAfter: true);
        }

        public void LogSubsection(string format, params object[] args)
        {
            var message = string.Format(format, args).Trim() + " ";
            var paddedMessage =
                message.PadRight(SubsectionLinesPadLength,
                    SubsectionLinesPadString);

            LogInfo(paddedMessage, newLineAfter: true);
        }

        // Header and Footer

        public void LogHeader(string format, bool addStart = false,
            params object[] args)
        {
            if (addStart)
                format += " - Start";
            var message = string.Format(format, args).Trim() + " ";
            var paddedMessage =
                message.PadRight(HeaderLinesPadLength, HeaderLinesPadString);

            LogInfo(paddedMessage, newLineAfter: true);
        }

        public void LogFooter(string format, bool addEnd = false,
            params object[] args)
        {
            if (addEnd)
                format += " - End";
            var message = string.Format(format, args).Trim() + " ";
            var paddedMessage =
                message.PadRight(FooterLinesPadLength, FooterLinesPadString);

            LogInfo(paddedMessage, newLineAfter: true);
        }

        #endregion

        #region Private Methods

        // TODO Go back to allowing multiple levels like the old FL library
        private static string AddIndent(bool addExtraIndent)
        {
            // Explicit ident for alignment plus optional one for nesting
            return Indent + (addExtraIndent ? Indent : "");
        }

        #endregion
    }
}