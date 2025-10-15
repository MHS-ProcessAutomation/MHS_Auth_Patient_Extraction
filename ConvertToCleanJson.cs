/*
 * UiPath Extraction Transformer
 * 
 * This class transforms UiPath Document Understanding extraction results
 * into a clean, hierarchical JSON structure while preserving original field names.
 * 
 * Usage:
 * var result = UMDelegationPerformer.UiPathExtractionTransformer.TransformExtractionResults(jarrResults.ToString());
 * var result = UMDelegationPerformer.UiPathExtractionTransformer.TransformExtractionResults(jarrResults.ToString(), false);
 * 
 * Parameters:
 * - uiPathJsonString: The raw UiPath extraction JSON string
 * - includeMissingFields: If true, includes all fields from taxonomy (extracted + missing). If false, only extracted fields. Default: true
 * 
 * Returns:
 * - string: The transformed structured JSON
 * 
 * Example Output Structure (includeMissingFields = true):
 * {
 *   "DocumentId": "1580d619-428f-f011-b484-000d3a57b549",
 *   "DocumentType": "Default",
 *   "Language": "eng",
 *   "ProcessedDateTime": "2024-09-11T...",
 *   "FileDetails": {
 *     "LocalPath": "C:\\Users\\asd\\Downloads\\fielname.PDF",
 *     "FullName": "fielname",
 *     "Extension": ".PDF",
 *     "PageRange": {
 *       "StartPage": 0,
 *       "PageCount": 5,
 *       "TextStartIndex": 0,
 *       "TextLength": 8540,
 *       "PageRange": "1-5"
 *     }
 *   },
 *   "Data": {
 *     "General": [{
 *       "Vendor ": { "Value": "WellMed", "Confidence": 0.8499, "OcrConfidence": 0.99, "IsExtracted": true },
 *       "Facility": { "Value": "Methodist Richardson Medical Center", "Confidence": 0.9998, "OcrConfidence": 0.99, "IsExtracted": true }
 *     }],
 *     "General > Wellmed": [{
 *       "Notice of approval of request for services": { "Value": "False", "Confidence": 0.9627, "OcrConfidence": -1.0, "IsExtracted": true },
 *       "Approved: Service requested is covered by your plan": { "Value": "True", "Confidence": 0.7607, "OcrConfidence": 0.97, "IsExtracted": true }
 *     }],
 *     "General > United Healthcare": [{
 *       "Notice of approval": { "Value": null, "Confidence": -1.0, "OcrConfidence": -1.0, "IsExtracted": false },
 *       "Notice of adverse determination": { "Value": null, "Confidence": -1.0, "OcrConfidence": -1.0, "IsExtracted": false },
 *       "Notice of Adverse Benefit Determination": { "Value": null, "Confidence": -1.0, "OcrConfidence": -1.0, "IsExtracted": false },
 *       "Inferred Free text": { "Value": null, "Confidence": -1.0, "OcrConfidence": -1.0, "IsExtracted": false }
 *     }]
 *   }
 * }
 * 
 * Example Output Structure (includeMissingFields = false):
 * {
 *   "DocumentId": "1580d619-428f-f011-b484-000d3a57b549",
 *   "DocumentType": "Default",
 *   "Language": "eng",
 *   "ProcessedDateTime": "2024-09-11T...",
 *   "FileDetails": { ... },
 *   "Data": {
 *     "General": [{
 *       "Vendor ": { "Value": "WellMed", "Confidence": 0.8499, "OcrConfidence": 0.99, "IsExtracted": true },
 *       "Facility": { "Value": "Methodist Richardson Medical Center", "Confidence": 0.9998, "OcrConfidence": 0.99, "IsExtracted": true }
 *     }],
 *     "General > Wellmed": [{
 *       "Notice of approval of request for services": { "Value": "False", "Confidence": 0.9627, "OcrConfidence": -1.0, "IsExtracted": true },
 *       "Approved: Service requested is covered by your plan": { "Value": "True", "Confidence": 0.7607, "OcrConfidence": 0.97, "IsExtracted": true }
 *     }]
 *   }
 * }
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UiPath.CodedWorkflows;
using UiPath.Core;
using UiPath.Core.Activities.Storage;
using UiPath.Excel;
using UiPath.Excel.Activities;
using UiPath.Excel.Activities.API;
using UiPath.Excel.Activities.API.Models;
using UiPath.Orchestrator.Client.Models;
using UiPath.Testing;
using UiPath.Testing.Activities.Api.Models;
using UiPath.Testing.Activities.Models;
using UiPath.Testing.Activities.TestData;
using UiPath.Testing.Activities.TestDataQueues.Enums;
using UiPath.Testing.Enums;
using UiPath.UIAutomationNext.API.Contracts;
using UiPath.UIAutomationNext.API.Models;
using UiPath.UIAutomationNext.Enums;

namespace UM_Auth_PatientExtraction
{
    public class FieldValue
    {
        public string Value { get; set; }
        public double Confidence { get; set; }
        public double OcrConfidence { get; set; }
        public bool IsExtracted { get; set; }
    }

    public class UiPathExtractionTransformer
    {
        private static IEnumerable<JToken> AsEnumerableValues(JToken token)
        {
            if (token == null)
            {
                return Enumerable.Empty<JToken>();
            }
            if (token.Type == JTokenType.Array)
            {
                return token.Children();
            }
            if (token.Type == JTokenType.Object)
            {
                return new[] { token };
            }
            return Enumerable.Empty<JToken>();
        }

        private static double ReadDoubleOrDefault(JToken token, double defaultValue)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return defaultValue;
            }
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                try { return token.ToObject<double>(); } catch { return defaultValue; }
            }
            var str = token.ToString();
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        private static string SafeGetString(JToken parent, string propertyName)
        {
            if (parent == null) return null;
            if (parent.Type != JTokenType.Object) return null;
            var child = parent[propertyName];
            if (child == null || child.Type == JTokenType.Null) return null;
            return child.ToString();
        }

        public static string TransformExtractionResults(string uiPathJsonString, bool includeMissingFields = true)
        {
            string stage = "Start";
            try
            {
                if (string.IsNullOrEmpty(uiPathJsonString))
                {
                    return CreateErrorJson("Input JSON string is null or empty", "InputValidation", nameof(ArgumentException), null);
                }

                stage = "ParseRoot";
                var root = JObject.Parse(uiPathJsonString);
                
                stage = "LocateExtractionResults";
                var properties = root["Properties"];
                var extractionResult = properties?.Type == JTokenType.Object ? properties?["ExtractionResult"] : null;
                var resultsDocument = extractionResult?.Type == JTokenType.Object ? extractionResult?["ResultsDocument"] : null;
                
                // Create the hierarchical data structure
                var data = new Dictionary<string, object>();
                
                // First, process extracted fields
                stage = "ProcessExtractedFields";
                if (resultsDocument != null && resultsDocument.Type == JTokenType.Object)
                {
                    var fieldsToken = resultsDocument["Fields"];
                    IEnumerable<JToken> fields = AsEnumerableValues(fieldsToken);
                    if (fields != null)
                    {
                        foreach (var field in fields)
                        {
                            ProcessFieldGroup(field, data);
                        }
                    }
                }
                
                // Then, add missing/unextracted fields from taxonomy (if requested)
                stage = "ProcessTaxonomyMissingFields";
                if (includeMissingFields)
                {
                    var taxonomy = properties?.Type == JTokenType.Object ? properties?["Taxonomy"] : null;
                    if (taxonomy != null && taxonomy.Type == JTokenType.Object)
                    {
                        ProcessMissingFields(taxonomy, data);
                    }
                }
                
                // Compute robust metadata fallbacks
                stage = "ComputeMetadata";
                var bounds = resultsDocument?["Bounds"]?.Type == JTokenType.Object ? resultsDocument?["Bounds"] : null;
                var fileDetails = root["FileDetails"]?.Type == JTokenType.Object ? root["FileDetails"] : null;
                var filePageRange = fileDetails?["PageRange"]?.Type == JTokenType.Object ? fileDetails?["PageRange"] : null;

                int? startPage = filePageRange?["StartPage"]?.ToObject<int?>() ?? bounds?["StartPage"]?.ToObject<int?>();
                int? pageCount = filePageRange?["PageCount"]?.ToObject<int?>() ?? bounds?["PageCount"]?.ToObject<int?>();
                int? textStartIndex = filePageRange?["TextStartIndex"]?.ToObject<int?>() ?? bounds?["TextStartIndex"]?.ToObject<int?>();
                int? textLength = filePageRange?["TextLength"]?.ToObject<int?>() ?? bounds?["TextLength"]?.ToObject<int?>();
                var pageRangeString = filePageRange?["PageRange"]?.ToString();
                if (string.IsNullOrEmpty(pageRangeString))
                {
                    pageRangeString = bounds?["PageRange"]?.ToString();
                }

                var documentId = SafeGetString(extractionResult, "DocumentId");
                if (string.IsNullOrEmpty(documentId))
                {
                    var dom = root["DocumentMetadata"]?.Type == JTokenType.Object ? root["DocumentMetadata"]?["DocumentObjectModel"] : null;
                    documentId = SafeGetString(dom, "DocumentId");
                }

                var documentTypeName = SafeGetString(resultsDocument, "DocumentTypeName");
                if (string.IsNullOrEmpty(documentTypeName))
                {
                    documentTypeName = SafeGetString(root["DocumentType"], "Name");
                }

                var language = SafeGetString(resultsDocument, "Language");
                if (string.IsNullOrEmpty(language))
                {
                    language = SafeGetString(root["DocumentMetadata"], "Language");
                }
                
                var extractorId = SafeGetString(resultsDocument, "ExtractorId");
                if (string.IsNullOrEmpty(extractorId))
                {
                    extractorId = SafeGetString(root["Properties"], "ExtractorId");
                }
                

                // Create the final output structure
                stage = "BuildOutput";
                var output = new
                {
                    DocumentId = documentId,
                    DocumentType = documentTypeName,
                    Language = language,
                    ExtractorId = extractorId,
                    ProcessedDateTime = DateTime.UtcNow.ToString("o"),
                    FileDetails = new
                    {
                        LocalPath = SafeGetString(fileDetails, "LocalPath"),
                        FullName = SafeGetString(fileDetails, "FullName"),
                        Extension = SafeGetString(fileDetails, "Extension"),
                        PageRange = new
                        {
                            StartPage = startPage,
                            PageCount = pageCount,
                            TextStartIndex = textStartIndex,
                            TextLength = textLength,
                            PageRange = pageRangeString
                        }
                    },
                    Data = data
                };
                
                // Serialize to JSON with proper formatting
                stage = "Serialize";
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include
                };
                
                return JsonConvert.SerializeObject(output, settings);
            }
            catch (Exception ex)
            {
                return CreateErrorJson($"Error processing extraction results: {ex.Message}", stage, ex.GetType().Name, ex.ToString());
            }
        }

        private static string CreateErrorJson(string errorMessage)
        {
            return CreateErrorJson(errorMessage, null, null, null);
        }

        private static string CreateErrorJson(string errorMessage, string errorStage, string exceptionType, string exceptionStack)
        {
            var errorOutput = new
            {
                DocumentId = "",
                DocumentType = "",
                Language = "",
                FileName = "",
                ProcessedDateTime = DateTime.UtcNow.ToString("o"),
                ErrorMessage = errorMessage,
                ErrorStage = errorStage,
                ExceptionType = exceptionType,
                Exception = exceptionStack,
                Data = new Dictionary<string, object>()
            };
            
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };
            
            return JsonConvert.SerializeObject(errorOutput, settings);
        }
        
        private static void ProcessFieldGroup(JToken field, Dictionary<string, object> data)
        {
            if (field == null || field.Type != JTokenType.Object) return;
            var fieldName = field["FieldName"]?.ToString();
            var fieldId = field["FieldId"]?.ToString();
            
            if (string.IsNullOrEmpty(fieldName)) return;
            
            // Keep original field name as group name
            var groupName = fieldName;
            
            var valuesToken = field["Values"];
            foreach (var value in AsEnumerableValues(valuesToken))
            {
                var componentsToken = value["Components"];
                var groupData = new Dictionary<string, FieldValue>();
                foreach (var component in AsEnumerableValues(componentsToken))
                {
                    ProcessComponent(component, groupData);
                }
                if (groupData.Any())
                {
                    data[groupName] = new[] { groupData };
                }
            }
        }
        
        private static void ProcessComponent(JToken component, Dictionary<string, FieldValue> groupData)
        {
            if (component == null || component.Type != JTokenType.Object) return;
            var componentName = component["FieldName"]?.ToString();
            var componentId = component["FieldId"]?.ToString();
            
            if (string.IsNullOrEmpty(componentName)) return;
            
            var valuesToken = component["Values"];
            foreach (var value in AsEnumerableValues(valuesToken))
            {
                if (value.Type != JTokenType.Object) continue;
                var fieldValue = value["Value"]?.ToString();
                var confidence = ReadDoubleOrDefault(value["Confidence"], -1.0);
                var ocrConfidence = ReadDoubleOrDefault(value["OcrConfidence"], -1.0);

                if (!string.IsNullOrEmpty(fieldValue) &&
                    fieldValue != componentName &&
                    fieldValue != componentId &&
                    confidence >= 0)
                {
                    groupData[componentName] = new FieldValue
                    {
                        Value = fieldValue,
                        Confidence = confidence,
                        OcrConfidence = ocrConfidence,
                        IsExtracted = true
                    };
                }

                var nestedComponentsToken = value["Components"];
                foreach (var nested in AsEnumerableValues(nestedComponentsToken))
                {
                    ProcessComponent(nested, groupData);
                }
            }
        }

        private static void ProcessMissingFields(JToken taxonomy, Dictionary<string, object> data)
        {
            if (taxonomy == null || taxonomy.Type != JTokenType.Object) return;
            var documentTypesToken = taxonomy["DocumentTypes"];
            foreach (var docType in AsEnumerableValues(documentTypesToken))
            {
                if (docType == null || docType.Type != JTokenType.Object) continue;
                var fieldsToken = docType["Fields"];

                foreach (var field in AsEnumerableValues(fieldsToken))
                {
                    if (field == null || field.Type != JTokenType.Object) continue;
                    var fieldName = field["FieldName"]?.ToString();
                    if (string.IsNullOrEmpty(fieldName)) continue;

                    // Check if this field group already exists in extracted data
                    if (data.ContainsKey(fieldName)) continue;

                    // Create missing field group with null values
                    var missingGroupData = new Dictionary<string, FieldValue>();
                    var componentsToken = field["Components"];
                    foreach (var component in AsEnumerableValues(componentsToken))
                    {
                        if (component == null || component.Type != JTokenType.Object) continue;
                        var componentName = component["FieldName"]?.ToString();
                        if (!string.IsNullOrEmpty(componentName))
                        {
                            missingGroupData[componentName] = new FieldValue
                            {
                                Value = null,
                                Confidence = -1.0,
                                OcrConfidence = -1.0,
                                IsExtracted = false
                            };
                        }
                    }

                    // Add the missing field group if it has components
                    if (missingGroupData.Any())
                    {
                        data[fieldName] = new[] { missingGroupData };
                    }
                }
            }
        }
        
    }
}