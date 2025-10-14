using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace UM_DelegationPerformer
{
    public class ConvertForMasterLog
    {
        // Define the expected schema with all possible fields
        private static readonly Dictionary<string, List<string>> ExpectedSchema = new Dictionary<string, List<string>>
        {
            { "General", new List<string> { "Vendor", "Facility", "Document Type" } },
            { "Patient Data", new List<string> { "Patient Name", "Patient Date of Birth", "Member ID" } }
        };

        private static readonly List<string> ExpectedProperties = new List<string> 
        { 
            "Value", "Confidence", "OcrConfidence", "IsExtracted" 
        };

        /// <summary>
        /// Converts JSON Data node to CSV header string based on expected schema
        /// </summary>
        public static string GetCsvHeaders(string json)
        {
            var headers = new List<string>();

            foreach (var section in ExpectedSchema)
            {
                foreach (var fieldName in section.Value)
                {
                    foreach (var prop in ExpectedProperties)
                    {
                        headers.Add($"{fieldName} {prop}");
                    }
                }
            }

            return string.Join(";", headers);
        }

        /// <summary>
        /// Converts JSON Data node to CSV row string with empty fields for missing data
        /// </summary>
        public static string ConvertJsonToCsvRow(string json)
        {
            var root = JObject.Parse(json);
            var values = new List<string>();

            var dataSections = (JObject)root["Data"];
            
            foreach (var section in ExpectedSchema)
            {
                var sectionName = section.Key;
                var fieldNames = section.Value;

                JArray sectionData = null;
                JObject firstItem = null;

                // Try to get the section data
                if (dataSections != null && dataSections[sectionName] != null)
                {
                    sectionData = (JArray)dataSections[sectionName];
                    if (sectionData != null && sectionData.Count > 0)
                    {
                        firstItem = (JObject)sectionData[0];
                    }
                }

                // Process each expected field
                foreach (var fieldName in fieldNames)
                {
                    JObject fieldObj = null;

                    // Try to get the field object
                    if (firstItem != null && firstItem[fieldName] != null)
                    {
                        fieldObj = (JObject)firstItem[fieldName];
                    }

                    // Extract each expected property
                    foreach (var propName in ExpectedProperties)
                    {
                        string value = string.Empty;

                        if (fieldObj != null && fieldObj[propName] != null)
                        {
                            value = fieldObj[propName].ToString();
                        }

                        // Escape the value for CSV
                        value = EscapeCsvValue(value);
                        values.Add(value);
                    }
                }
            }

            return string.Join(";", values);
        }

        /// <summary>
        /// Escapes CSV values containing special characters
        /// </summary>
        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // If value contains semicolon, quote, or newline, wrap in quotes
            if (value.Contains(";") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                // Escape quotes by doubling them
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return value;
        }

        /// <summary>
        /// Gets both headers and row data for easy CSV creation
        /// </summary>
        public static (string headers, string row) GetCsvData(string json)
        {
            return (GetCsvHeaders(json), ConvertJsonToCsvRow(json));
        }

        /// <summary>
        /// Returns headers and row as Dictionary for UiPath
        /// Access with result("Headers") and result("Row")
        /// </summary>
        public static Dictionary<string, string> GetCsvDataAsDictionary(string json)
        {
            var result = new Dictionary<string, string>();
            result.Add("Headers", GetCsvHeaders(json));
            result.Add("Row", ConvertJsonToCsvRow(json));
            return result;
        }

        /// <summary>
        /// Converts JSON to DataTable for UiPath
        /// Creates columns from headers and adds one row with values
        /// </summary>
        public static DataTable ConvertJsonToDataTable(string json)
        {
            var dt = new DataTable();
            
            var headers = GetCsvHeaders(json);
            var row = ConvertJsonToCsvRow(json);
            
            // Split headers and create columns
            var headerArray = headers.Split(';');
            foreach (var header in headerArray)
            {
                dt.Columns.Add(header.Trim());
            }
            
            // Split row values and add data row
            var values = row.Split(';');
            var dataRow = dt.NewRow();
            
            for (int i = 0; i < headerArray.Length; i++)
            {
                if (i < values.Length)
                {
                    // Remove quotes if value is escaped
                    var value = values[i].Trim();
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
                    }
                    dataRow[i] = value;
                }
                else
                {
                    // Populate empty if value is missing
                    dataRow[i] = string.Empty;
                }
            }
            
            dt.Rows.Add(dataRow);
            return dt;
        }
    }
}