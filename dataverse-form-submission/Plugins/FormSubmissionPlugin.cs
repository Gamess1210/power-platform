using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseFormSubmission.Plugins
{
    /// <summary>
    /// Plugin that creates or updates a record in a Dataverse table
    /// when a form submission is received. Register this plugin on the
    /// Create message of the source entity (e.g., "new_formsubmission").
    ///
    /// Registration steps (using Plugin Registration Tool):
    ///   1. Register the assembly.
    ///   2. Register a step on Message "Create" for entity "new_formsubmission"
    ///      in the Post-Operation stage (synchronous or asynchronous).
    ///   3. Optionally register a step on Message "Update" for the same entity
    ///      to handle edits to existing submissions.
    /// </summary>
    public class FormSubmissionPlugin : IPlugin
    {
        // Target table where aggregated/processed data is stored.
        private const string TargetTableLogicalName = "new_submissionsummary";

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters == null ||
                !context.InputParameters.Contains("Target") ||
                !(context.InputParameters["Target"] is Entity submittedForm))
            {
                tracingService.Trace("Target entity not found in input parameters.");
                return;
            }

            try
            {
                tracingService.Trace("FormSubmissionPlugin: Processing form submission {0}.", submittedForm.Id);

                string submitterEmail = submittedForm.GetAttributeValue<string>("new_email");
                string submitterName = submittedForm.GetAttributeValue<string>("new_fullname");
                string formType = submittedForm.GetAttributeValue<string>("new_formtype");
                string responseData = submittedForm.GetAttributeValue<string>("new_responsedata");

                // Check whether a summary record already exists for this submitter.
                Entity existingRecord = FindExistingRecord(service, submitterEmail);

                if (existingRecord != null)
                {
                    UpdateExistingRecord(service, tracingService, existingRecord, submitterName, formType, responseData);
                }
                else
                {
                    CreateNewRecord(service, tracingService, submitterEmail, submitterName, formType, responseData);
                }

                // Mark the source submission as processed.
                var updateSubmission = new Entity(submittedForm.LogicalName, submittedForm.Id);
                updateSubmission["new_processed"] = true;
                updateSubmission["new_processedon"] = DateTime.UtcNow;
                service.Update(updateSubmission);

                tracingService.Trace("FormSubmissionPlugin: Completed successfully.");
            }
            catch (Exception ex)
            {
                tracingService.Trace("FormSubmissionPlugin error: {0}", ex.ToString());
                throw new InvalidPluginExecutionException(
                    $"An error occurred in FormSubmissionPlugin: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Looks up an existing summary record by submitter email.
        /// Returns null if no match is found.
        /// </summary>
        private static Entity FindExistingRecord(IOrganizationService service, string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            var query = new QueryExpression(TargetTableLogicalName)
            {
                ColumnSet = new ColumnSet("new_submissioncount", "new_lastformtype", "new_lastsubmittedon"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("new_email", ConditionOperator.Equal, email),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0) // Active records only
                    }
                },
                TopCount = 1
            };

            var results = service.RetrieveMultiple(query);
            return results.Entities.Count > 0 ? results.Entities[0] : null;
        }

        /// <summary>
        /// Updates the existing summary record with the latest submission data
        /// and increments the submission counter.
        /// </summary>
        private static void UpdateExistingRecord(
            IOrganizationService service,
            ITracingService tracingService,
            Entity existing,
            string submitterName,
            string formType,
            string responseData)
        {
            tracingService.Trace("Updating existing record {0}.", existing.Id);

            int currentCount = existing.GetAttributeValue<int>("new_submissioncount");

            var update = new Entity(TargetTableLogicalName, existing.Id);
            update["new_fullname"] = submitterName;
            update["new_lastformtype"] = formType;
            update["new_lastresponsedata"] = responseData;
            update["new_submissioncount"] = currentCount + 1;
            update["new_lastsubmittedon"] = DateTime.UtcNow;

            service.Update(update);

            tracingService.Trace("Record {0} updated. Submission count: {1}.", existing.Id, currentCount + 1);
        }

        /// <summary>
        /// Creates a new summary record for a first-time submitter.
        /// </summary>
        private static void CreateNewRecord(
            IOrganizationService service,
            ITracingService tracingService,
            string email,
            string submitterName,
            string formType,
            string responseData)
        {
            tracingService.Trace("Creating new summary record for {0}.", email);

            var newRecord = new Entity(TargetTableLogicalName);
            newRecord["new_email"] = email;
            newRecord["new_fullname"] = submitterName;
            newRecord["new_lastformtype"] = formType;
            newRecord["new_lastresponsedata"] = responseData;
            newRecord["new_submissioncount"] = 1;
            newRecord["new_lastsubmittedon"] = DateTime.UtcNow;

            Guid newId = service.Create(newRecord);

            tracingService.Trace("Created new record {0}.", newId);
        }
    }
}
