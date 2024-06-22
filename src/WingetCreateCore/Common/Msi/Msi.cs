// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Microsoft.WingetCreateCore.Common.Msi
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    public class Msi
    {
        public unsafe Msi(string path)
        {
            fixed (byte* pathPtr = Encoding.UTF8.GetBytes(path))
            {
                MsiInformationFfi info = get_information(pathPtr);
                this.Information = new MsiInformation
                {
                    Architecture = GetString(info.arch),
                    Author = GetString(info.author),
                    Comments = GetString(info.comments),
                    CreatingApplication = GetString(info.creating_application),
                    CreationTime = GetString(info.creation_time),
                    Languages = GetStringArray(info.languages),
                    Subject = GetString(info.subject),
                    Title = GetString(info.title),
                    Uuid = GetString(info.uuid),
                    WordCount = info.word_count,
                    HasDigitalSignature = info.has_digital_signature,
                    TableNames = GetStringArray(info.table_names),
                };
                free_information(info);

                this.Tables = new MsiTable[this.Information.TableNames.Length];
                for (int i = 0; i < this.Information.TableNames.Length; i++)
                {
                    fixed (byte* tableNamePtr = Encoding.UTF8.GetBytes(this.Information.TableNames[i]))
                    {
                        String2DArrayFfi table_ffi = get_table(pathPtr, tableNamePtr);
                        string[][] table = GetString2DArray(table_ffi);
                        this.Tables[i] = new MsiTable
                        {
                            Name = this.Information.TableNames[i],
                            Columns = table[0],
                            Rows = table[1..],
                        };
                        free_table(table_ffi);
                    }
                }
            }
        }

        public MsiInformation Information { get; private set; }

        public MsiTable[] Tables { get; private set; }

        [DllImport("msi", ExactSpelling = true)]
        private static unsafe extern MsiInformationFfi get_information(byte* path);

        [DllImport("msi", ExactSpelling = true)]
        private static unsafe extern String2DArrayFfi get_table(byte* path, byte* table_name);

        [DllImport("msi", ExactSpelling = true)]
        private static unsafe extern void free_information(MsiInformationFfi info);

        [DllImport("msi", ExactSpelling = true)]
        private static unsafe extern void free_table(String2DArrayFfi table);

        private static unsafe string GetString(StringFfi s)
        {
            try
            {
                byte[] byteArray = new byte[s.len.ToUInt32()];
                Marshal.Copy((IntPtr)s.ptr, byteArray, 0, (int)s.len);
                return Encoding.UTF8.GetString(byteArray);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static unsafe string[] GetStringArray(StringArrayFfi x)
        {
            string[] result = new string[x.len.ToUInt32()];
            for (int i = 0; i < result.Length; i++)
            {
                int offset = i * Marshal.SizeOf<StringFfi>();
                result[i] = GetString(Marshal.PtrToStructure<StringFfi>((IntPtr)x.ptr + offset));
            }

            return result;
        }

        private static unsafe string[][] GetString2DArray(String2DArrayFfi x)
        {
            string[][] result = new string[x.len.ToUInt32()][];
            for (int i = 0; i < result.Length; i++)
            {
                int offset = i * Marshal.SizeOf<StringArrayFfi>();
                result[i] = GetStringArray(Marshal.PtrToStructure<StringArrayFfi>((IntPtr)x.ptr + offset));
            }

            return result;
        }

        public struct MsiInformation
        {
            public string Architecture;
            public string Author;
            public string Comments;
            public string CreatingApplication;
            public string CreationTime;
            public string[] Languages;
            public string Subject;
            public string Title;
            public string Uuid;
            public int WordCount;
            public bool HasDigitalSignature;
            public string[] TableNames;
        }

        public struct MsiTable
        {
            public string Name;
            public string[] Columns;
            public string[][] Rows;
        }

        [StructLayout(LayoutKind.Sequential, Size = 24)]
        private unsafe struct StringFfi
        {
            public byte* ptr;
            public UIntPtr len;
            public UIntPtr cap;
        }

        [StructLayout(LayoutKind.Sequential, Size = 24)]
        private unsafe struct StringArrayFfi
        {
            public StringFfi* ptr;
            public UIntPtr len;
            public UIntPtr cap;
        }

        [StructLayout(LayoutKind.Sequential, Size = 24)]
        private unsafe struct String2DArrayFfi
        {
            public StringArrayFfi* ptr;
            public UIntPtr len;
            public UIntPtr cap;
        }

        [StructLayout(LayoutKind.Sequential, Size = 248)]
        private struct MsiInformationFfi
        {
            public StringFfi arch;
            public StringFfi author;
            public StringFfi comments;
            public StringFfi creating_application;
            public StringFfi creation_time;
            public StringArrayFfi languages;
            public StringFfi subject;
            public StringFfi title;
            public StringFfi uuid;
            public int word_count;

            [MarshalAs(UnmanagedType.U1)]
            public bool has_digital_signature;

            public StringArrayFfi table_names;
        }
    }
}
