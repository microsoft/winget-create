// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.IO;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Interfaces;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Serializers;
    using NUnit.Framework;

    /// <summary>
    /// Unit tests for verifying unicode text and directionality.
    /// </summary>
    public class CharacterValidationTests
    {
        private readonly string[] testStrings =
        {
            "İkşzlerAçık芲偁ＡＢＣ巢für नमस्ते กุ้งจิ้яЧчŠš",
            "丂令龥€￥",
            "㐀㲷䶵",
            "𠀀𠀁𠀂",
            "أنا اختبار إدخال النص في لغات مختلفة 01 لأحد منتجات Microsoft",
            "freistoß für böse",
            "とよた小百合俊晴㊞ソ十申暴構能雲契活神点農ボ施倍府本宮マ笠急党図迎 ミ円救",
            "กุ้งจิ้มน้ปลาตั้งจเรียน",
            "नमस्ते धन्यवाद",
            "도망각하갂",
        };

        /// <summary>
        /// Verifies text support for unicode characters.
        /// </summary>
        [Test]
        public void VerifyTextSupport()
        {
            foreach (var serializerType in Serialization.AvailableSerializerTypes)
            {
                IManifestSerializer serializer = (IManifestSerializer)Activator.CreateInstance(serializerType);
                string testManifestFilePath = Path.Combine(Path.GetTempPath(), $"TestManifest{serializer.AssociatedFileExtension}");
                foreach (string testString in this.testStrings)
                {
                    SingletonManifest manifest = new SingletonManifest { Description = testString };
                    File.WriteAllText(testManifestFilePath, serializer.ToManifestString(manifest));

                    SingletonManifest testManifest = Serialization.DeserializeFromPath<SingletonManifest>(testManifestFilePath);
                    Assert.AreEqual(
                        testString,
                        testManifest.Description,
                        string.Format("Unicode string: {0} failed to display correctly.", testString));
                    File.Delete(testManifestFilePath);
                }
            }
        }

        /// <summary>
        /// Verifies that we aren't adding more newlines than expected.
        /// </summary>
        [Test]
        public void VerifyNewLineSupport()
        {
            string[] stringsWithNewLines =
            {
                "This\n has\n some newlines.",
                "So\r\n does this.",
                "And this does\x85.",
                "As does this\x2028.",
                "Me too!\x2029:)",
            };

            foreach (var serializerType in Serialization.AvailableSerializerTypes)
            {
                IManifestSerializer serializer = (IManifestSerializer)Activator.CreateInstance(serializerType);
                string testManifestFilePath = Path.Combine(Path.GetTempPath(), $"TestManifest{serializer.AssociatedFileExtension}");

                foreach (var i in stringsWithNewLines)
                {
                    SingletonManifest written = new SingletonManifest { Description = i };
                    File.WriteAllText(testManifestFilePath, serializer.ToManifestString(written));
                    SingletonManifest read = Serialization.DeserializeFromPath<SingletonManifest>(testManifestFilePath);

                    if (serializer.GetType() == typeof(YamlSerializer))
                    {
                        // we know that \r\n and \x85 characters are replaced with \n by YamlDotNet library.
                        var writtenFixed = string.Join('\n', written.Description.Split(new string[] { "\n", "\r\n", "\x85" }, StringSplitOptions.None));
                        Assert.AreEqual(writtenFixed, read.Description, $"String {read.Description} had the wrong number of newlines :(.");
                    }
                    else
                    {
                        Assert.AreEqual(written.Description, read.Description, $"String {read.Description} had the wrong number of newlines :(.");
                    }

                    File.Delete(testManifestFilePath);
                }
            }
        }
    }
}
