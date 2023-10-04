// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    /* This implementation is taken from the CommandLineParser reference examples.
     * https://github.com/commandlineparser/commandline/tree/master/demo/ReadText.LocalizedDemo
     */

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommandLine;
    using CommandLine.Text;

    /// <inheritdoc/>
    public class LocalizableSentenceBuilder : SentenceBuilder
    {
        /// <inheritdoc/>
        public override Func<string> RequiredWord
        {
            get { return () => Properties.Resources.SentenceRequiredWord; }
        }

        /// <inheritdoc/>
        public override Func<string> ErrorsHeadingText
        {
            // Cannot be pluralized
            get { return () => Properties.Resources.SentenceErrorsHeadingText; }
        }

        /// <inheritdoc/>
        public override Func<string> UsageHeadingText
        {
            get { return () => Properties.Resources.SentenceUsageHeadingText; }
        }

        /// <inheritdoc/>
        public override Func<string> OptionGroupWord
        {
            get { return () => Properties.Resources.SentenceOptionGroupWord; }
        }

        /// <inheritdoc/>
        public override Func<bool, string> HelpCommandText
        {
            get
            {
                return isOption => isOption
                    ? Properties.Resources.SentenceHelpCommandTextOption
                    : Properties.Resources.SentenceHelpCommandTextVerb;
            }
        }

        /// <inheritdoc/>
        public override Func<bool, string> VersionCommandText
        {
            get { return _ => Properties.Resources.SentenceVersionCommandText; }
        }

        /// <inheritdoc/>
        public override Func<Error, string> FormatError
        {
            get
            {
                return error =>
                {
                    switch (error.Tag)
                    {
                        case ErrorType.BadFormatTokenError:
                            return string.Format(Properties.Resources.SentenceBadFormatTokenError, ((BadFormatTokenError)error).Token);

                        case ErrorType.MissingValueOptionError:
                            return string.Format(Properties.Resources.SentenceMissingValueOptionError, ((MissingValueOptionError)error).NameInfo.NameText);

                        case ErrorType.UnknownOptionError:
                            return string.Format(Properties.Resources.SentenceUnknownOptionError, ((UnknownOptionError)error).Token);

                        case ErrorType.MissingRequiredOptionError:
                            var errMisssing = (MissingRequiredOptionError)error;
                            return errMisssing.NameInfo.Equals(NameInfo.EmptyName) ? Properties.Resources.SentenceMissingRequiredValueError
                                       : string.Format(Properties.Resources.SentenceMissingRequiredOptionError, errMisssing.NameInfo.NameText);

                        case ErrorType.BadFormatConversionError:
                            var badFormat = (BadFormatConversionError)error;
                            return badFormat.NameInfo.Equals(NameInfo.EmptyName) ? Properties.Resources.SentenceBadFormatConversionErrorValue
                                       : string.Format(Properties.Resources.SentenceBadFormatConversionErrorOption, badFormat.NameInfo.NameText);

                        case ErrorType.SequenceOutOfRangeError:
                            var seqOutRange = (SequenceOutOfRangeError)error;
                            return seqOutRange.NameInfo.Equals(NameInfo.EmptyName) ? Properties.Resources.SentenceSequenceOutOfRangeErrorValue
                                       : string.Format(Properties.Resources.SentenceSequenceOutOfRangeErrorOption, seqOutRange.NameInfo.NameText);

                        case ErrorType.BadVerbSelectedError:
                            return string.Format(Properties.Resources.SentenceBadVerbSelectedError, ((BadVerbSelectedError)error).Token);

                        case ErrorType.NoVerbSelectedError:
                            return Properties.Resources.SentenceNoVerbSelectedError;

                        case ErrorType.RepeatedOptionError:
                            return string.Format(Properties.Resources.SentenceRepeatedOptionError, ((RepeatedOptionError)error).NameInfo.NameText);

                        case ErrorType.SetValueExceptionError:
                            var setValueError = (SetValueExceptionError)error;
                            return string.Format(Properties.Resources.SentenceSetValueExceptionError, setValueError.NameInfo.NameText, setValueError.Exception.Message);
                    }

                    throw new InvalidOperationException();
                };
            }
        }

        /// <inheritdoc/>
        public override Func<IEnumerable<MutuallyExclusiveSetError>, string> FormatMutuallyExclusiveSetErrors
        {
            get
            {
                return errors =>
                {
                    var bySet = from e in errors
                                group e by e.SetName into g
                                select new { SetName = g.Key, Errors = g.ToList() };

                    var msgs = bySet.Select(
                        set =>
                        {
                            var names = string.Join(
                                string.Empty,
                                (from e in set.Errors select string.Format("'{0}', ", e.NameInfo.NameText)).ToArray());
                            var namesCount = set.Errors.Count();

                            var incompat = string.Join(
                                string.Empty,
                                (from x in
                                     (from s in bySet where !s.SetName.Equals(set.SetName) from e in s.Errors select e)
                                    .Distinct()
                                 select string.Format("'{0}', ", x.NameInfo.NameText)).ToArray());

                            // TODO: Pluralize by namesCount
                            return string.Format(
                                    Properties.Resources.SentenceMutuallyExclusiveSetErrors,
                                    names.Substring(0, names.Length - 2),
                                    incompat.Substring(0, incompat.Length - 2));
                        }).ToArray();
                    return string.Join(Environment.NewLine, msgs);
                };
            }
        }
    }
}
