using System;
using System.Collections.Generic;
using System.Data;
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
using UM_DelegationPerformer.ObjectRepository;
using Newtonsoft.Json.Linq;

namespace UM_DelegationPerformer
{
    public class ConvertJsonToDict
    {
        
    
    public static Dictionary<string, string> ConvertJsonToDictionary(string json)
    {
        var root = JObject.Parse(json);
        var result = new Dictionary<string, string>();

        // Loop through all sections in "Data"
        var dataSections = (JObject)root["Data"];
        foreach (var section in dataSections.Properties())
        {
            // Each section contains a list of items
            var items = (JArray)section.Value;
            foreach (JObject item in items)
            {
                foreach (var field in item.Properties())
                {
                    // Add field name and its Value to dictionary
                    var key = field.Name;
                    var value = field.Value["Value"]?.ToString(); // safe null access
                    if (!result.ContainsKey(key))
                    {
                        result.Add(key, value);
                    }
                }
            }
        }

        return result;


        }
}}