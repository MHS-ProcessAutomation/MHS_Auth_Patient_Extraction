using System;
using System.Collections.Generic;
using System.Data;
using UiPath.Activities.System.Jobs.Coded;
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
    public partial class CodedWorkflow : CodedWorkflowBase
    {
        private Lazy<global::UM_Auth_PatientExtraction.WorkflowRunnerService> _workflowRunnerServiceLazy;
        private Lazy<ConnectionsManager> _connectionsManagerLazy;
        public CodedWorkflow()
        {
            _ = new System.Type[]{typeof(UiPath.Excel.Activities.API.IExcelService), typeof(UiPath.Testing.API.ITestingService), typeof(UiPath.Core.Activities.API.ISystemService), typeof(UiPath.UIAutomationNext.API.Contracts.IUiAutomationAppService)};
            _workflowRunnerServiceLazy = new Lazy<global::UM_Auth_PatientExtraction.WorkflowRunnerService>(() => new global::UM_Auth_PatientExtraction.WorkflowRunnerService(this.services));
#pragma warning disable
            _connectionsManagerLazy = new Lazy<ConnectionsManager>(() => new ConnectionsManager(serviceContainer));
#pragma warning restore
        }

        protected global::UM_Auth_PatientExtraction.WorkflowRunnerService workflows => _workflowRunnerServiceLazy.Value;
        protected ConnectionsManager connections => _connectionsManagerLazy.Value;
#pragma warning disable
        protected UiPath.Excel.Activities.API.IExcelService excel { get => serviceContainer.Resolve<UiPath.Excel.Activities.API.IExcelService>() ; }
#pragma warning restore

#pragma warning disable
        protected UiPath.Core.Activities.API.ISystemService system { get => serviceContainer.Resolve<UiPath.Core.Activities.API.ISystemService>() ; }
#pragma warning restore

#pragma warning disable
        protected UiPath.Testing.API.ITestingService testing { get => serviceContainer.Resolve<UiPath.Testing.API.ITestingService>() ; }
#pragma warning restore

#pragma warning disable
        protected UiPath.UIAutomationNext.API.Contracts.IUiAutomationAppService uiAutomation { get => serviceContainer.Resolve<UiPath.UIAutomationNext.API.Contracts.IUiAutomationAppService>() ; }
#pragma warning restore
    }
}