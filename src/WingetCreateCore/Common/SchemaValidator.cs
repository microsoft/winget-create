// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Schema;
    using YamlDotNet.Serialization;

    /// <summary>
    /// Provides functionality for validating a JSON schema.
    /// </summary>
    public class SchemaValidator
    {
        private readonly JSchema schema;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaValidator"/> class.
        /// </summary>
        /// <param name="schemaPath">Path to schema file. </param>
        public SchemaValidator(string schemaPath)
        {
            string schemaText = File.ReadAllText(schemaPath);
            this.schema = JSchema.Parse(schemaText);
        }

        /// <summary>
        /// Utilizes the specified schema to validate a given manifest.
        /// </summary>
        /// <param name="yaml">Yaml string to be validated. </param>
        /// <returns>bool representing the validity of the yaml string. </returns>
        public bool ValidateManifest(string yaml)
        {
            object obj;
            using (var reader = new StringReader(yaml))
            {
                var deserializer = new Deserializer();
                obj = deserializer.Deserialize(reader);
            }

            var json = JObject.FromObject(obj);
            bool isValid = json.IsValid(this.schema, out IList<string> errors);
            foreach (var error in errors)
            {
                Trace.TraceError(error.Replace(Environment.NewLine, " ").Replace('\n', ' '));
            }

            return isValid;
        }
    }
}
