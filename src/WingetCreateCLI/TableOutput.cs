// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Helper class to generate a formatted table.
    /// </summary>
    public class TableOutput
    {
        private readonly List<string> columns = new();
        private readonly List<List<string>> rows = new();
        private readonly int padding = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableOutput"/> class.
        /// Provide column names to generate a formatted table.
        /// </summary>
        /// <param name="columnNames">List of column names.</param>
        public TableOutput(params string[] columnNames)
        {
            this.columns.AddRange(columnNames);
        }

        /// <summary>
        /// Add a row to the table.
        /// </summary>
        /// <param name="rowData">List of row entries.</param>
        /// <returns>TableOutput object to allow chaining method calls.</returns>
        public TableOutput AddRow(params string[] rowData)
        {
            this.rows.Add(new List<string>(rowData));
            return this;
        }

        /// <summary>
        /// Write the table to the standard output.
        /// </summary>
        public void Print()
        {
            // Calculate the maximum width of each column.
            List<int> columnWidths = this.CalculateColumnWidths();

            // Print the column names.
            for (int i = 0; i < this.columns.Count; i++)
            {
                Console.Write("{0, -" + (columnWidths[i] + this.padding) + "}", this.columns[i]);
            }

            Console.WriteLine();

            // Print a line of dashes to separate the column names from the rows.
            Console.WriteLine(new string('-', columnWidths.Sum() + columnWidths.Count + this.padding));

            // Print the rows.
            foreach (var row in this.rows)
            {
                for (int i = 0; i < this.columns.Count; i++)
                {
                    Console.Write("{0, -" + (columnWidths[i] + this.padding) + "}", row[i]);
                }

                Console.WriteLine();
            }
        }

        private List<int> CalculateColumnWidths()
        {
            List<int> columnWidths = new List<int>();

            for (int i = 0; i < this.columns.Count; i++)
            {
                // Initially set the column width to the column name length.
                int maxLength = this.columns[i].Length;

                foreach (var row in this.rows)
                {
                    // Check if any row entry in the respective column is longer than the column name.
                    if (i < row.Count && row[i].Length > maxLength)
                    {
                        maxLength = row[i].Length;
                    }
                }

                columnWidths.Add(maxLength);
            }

            return columnWidths;
        }
    }
}
