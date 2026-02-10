// =============================================================================
// Power Fx – Canvas App Form Submission to Dataverse
// =============================================================================
// Use these formulas in a Canvas App to create or update records in a
// Dataverse table when a form is submitted. Attach the OnSuccess formula
// to a form control's OnSuccess property, and the submit trigger to a
// button's OnSelect property.
//
// Prerequisites:
//   - A Dataverse table named "Submission Summaries" connected as a data source.
//   - A form control (FormSubmission) bound to the "Form Submissions" table.
// =============================================================================

// ---------------------------------------------------------------------------
// 1. Button OnSelect – Trigger the form submission
// ---------------------------------------------------------------------------
// Place this formula on the Submit button's OnSelect property.
// It validates the form, submits it, and shows a notification on failure.

// SubmitButton.OnSelect:
SubmitForm(FormSubmission);

// ---------------------------------------------------------------------------
// 2. Form OnSuccess – Create or update the summary table
// ---------------------------------------------------------------------------
// Place this formula on the FormSubmission control's OnSuccess property.
// It fires only after the record is successfully saved to Dataverse.

// FormSubmission.OnSuccess:
With(
    {
        _email:    FormSubmission.LastSubmit.Email,
        _name:     FormSubmission.LastSubmit.'Full Name',
        _formType: FormSubmission.LastSubmit.'Form Type',
        _response: FormSubmission.LastSubmit.'Response Data'
    },

    // Look up an existing summary record for this submitter.
    With(
        {
            _existing: LookUp(
                'Submission Summaries',
                Email = _email && Status = 'Status (Submission Summaries)'.Active
            )
        },

        If(
            // --- UPDATE path: record already exists ---
            !IsBlank(_existing),
            Patch(
                'Submission Summaries',
                _existing,
                {
                    'Full Name':          _name,
                    'Last Form Type':     _formType,
                    'Last Response Data':  _response,
                    'Submission Count':    _existing.'Submission Count' + 1,
                    'Last Submitted On':   Now()
                }
            ),

            // --- CREATE path: first submission from this email ---
            Patch(
                'Submission Summaries',
                Defaults('Submission Summaries'),
                {
                    Email:                _email,
                    'Full Name':          _name,
                    'Last Form Type':     _formType,
                    'Last Response Data':  _response,
                    'Submission Count':    1,
                    'Last Submitted On':   Now()
                }
            )
        );

        // Mark the original submission as processed.
        Patch(
            'Form Submissions',
            FormSubmission.LastSubmit,
            {
                Processed:    true,
                'Processed On': Now()
            }
        );

        // Notify the user and reset the form for a new entry.
        Notify("Form submitted successfully!", NotificationType.Success);
        ResetForm(FormSubmission)
    )
);

// ---------------------------------------------------------------------------
// 3. Form OnFailure – Handle submission errors
// ---------------------------------------------------------------------------
// Place this on the FormSubmission control's OnFailure property.

// FormSubmission.OnFailure:
Notify(
    "Submission failed. Please check your entries and try again.",
    NotificationType.Error
);
