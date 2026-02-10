/**
 * Dataverse Form Submission Handler
 *
 * Client-side JavaScript web resource for model-driven app forms.
 * Handles the OnSave event to create or update a record in a separate
 * Dataverse table using the Web API after the form is successfully saved.
 *
 * Setup:
 *   1. Upload this file as a JavaScript web resource in your solution.
 *   2. Add the web resource to the form's Form Libraries.
 *   3. Configure the form OnSave event to call:
 *        FormSubmission.onFormSave
 */
var FormSubmission = (function () {
    "use strict";

    // The table where summary records are stored.
    var SUMMARY_TABLE = "new_submissionsummaries";

    /**
     * OnSave event handler. Fires after the form record is saved.
     * @param {Object} executionContext - The execution context from the form event.
     */
    function onFormSave(executionContext) {
        var formContext = executionContext.getFormContext();
        var saveMode = executionContext.getEventArgs().getSaveMode();

        // Save modes: 1 = Save, 2 = Save and Close, 59 = Save and New
        if (saveMode !== 1 && saveMode !== 2 && saveMode !== 59) {
            return;
        }

        // Only proceed if the save was successful (record has an id).
        var recordId = formContext.data.entity.getId();
        if (!recordId) {
            return;
        }

        var email = getFieldValue(formContext, "new_email");
        var fullName = getFieldValue(formContext, "new_fullname");
        var formType = getFieldValue(formContext, "new_formtype");
        var responseData = getFieldValue(formContext, "new_responsedata");

        findExistingRecord(email)
            .then(function (existing) {
                if (existing) {
                    return updateRecord(existing, fullName, formType, responseData);
                }
                return createRecord(email, fullName, formType, responseData);
            })
            .then(function () {
                markSubmissionProcessed(formContext, recordId);
            })
            .catch(function (error) {
                console.error("FormSubmission: Error processing submission", error);
                Xrm.Navigation.openAlertDialog({
                    text: "The form was saved, but an error occurred while updating the summary record: " + error.message
                });
            });
    }

    /**
     * Safely retrieves a field value from the form.
     */
    function getFieldValue(formContext, fieldName) {
        var attribute = formContext.getAttribute(fieldName);
        return attribute ? attribute.getValue() : null;
    }

    /**
     * Queries the summary table for an existing record matching the email.
     * @param {string} email - The submitter email to search for.
     * @returns {Promise<Object|null>} The existing record or null.
     */
    function findExistingRecord(email) {
        if (!email) {
            return Promise.resolve(null);
        }

        var filter = "?$filter=new_email eq '" + encodeURIComponent(email) + "' and statecode eq 0" +
                     "&$select=new_submissionsummaryid,new_submissioncount,new_lastformtype" +
                     "&$top=1";

        return Xrm.WebApi.retrieveMultipleRecords(SUMMARY_TABLE, filter)
            .then(function (result) {
                return result.entities.length > 0 ? result.entities[0] : null;
            });
    }

    /**
     * Creates a new summary record for a first-time submitter.
     */
    function createRecord(email, fullName, formType, responseData) {
        var record = {
            new_email: email,
            new_fullname: fullName,
            new_lastformtype: formType,
            new_lastresponsedata: responseData,
            new_submissioncount: 1,
            new_lastsubmittedon: new Date().toISOString()
        };

        return Xrm.WebApi.createRecord(SUMMARY_TABLE, record)
            .then(function (result) {
                console.log("FormSubmission: Created summary record " + result.id);
                return result;
            });
    }

    /**
     * Updates an existing summary record with the latest submission data.
     */
    function updateRecord(existing, fullName, formType, responseData) {
        var currentCount = existing.new_submissioncount || 0;

        var record = {
            new_fullname: fullName,
            new_lastformtype: formType,
            new_lastresponsedata: responseData,
            new_submissioncount: currentCount + 1,
            new_lastsubmittedon: new Date().toISOString()
        };

        var recordId = existing.new_submissionsummaryid;

        return Xrm.WebApi.updateRecord(SUMMARY_TABLE, recordId, record)
            .then(function (result) {
                console.log("FormSubmission: Updated summary record " + result.id +
                            " (count: " + (currentCount + 1) + ")");
                return result;
            });
    }

    /**
     * Marks the original form submission as processed.
     */
    function markSubmissionProcessed(formContext, recordId) {
        var entityName = formContext.data.entity.getEntityName();

        var update = {
            new_processed: true,
            new_processedon: new Date().toISOString()
        };

        // Strip braces from the record ID for the Web API call.
        var cleanId = recordId.replace(/[{}]/g, "");

        Xrm.WebApi.updateRecord(entityName, cleanId, update)
            .then(function () {
                console.log("FormSubmission: Marked submission " + cleanId + " as processed.");
                // Refresh the form to reflect the updated processed status.
                formContext.data.refresh(false);
            })
            .catch(function (error) {
                console.error("FormSubmission: Failed to mark submission as processed", error);
            });
    }

    // Public API
    return {
        onFormSave: onFormSave
    };
})();
