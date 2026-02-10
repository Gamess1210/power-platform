# Dataverse Form Submission – Create & Update Table on Submit

Three approaches for creating or updating a Dataverse table when a form is
successfully submitted in Power Platform.

## Approaches

### 1. C# Dataverse Plugin (server-side)

**File:** `Plugins/FormSubmissionPlugin.cs`

A server-side plugin registered on the **Create** message of the
`new_formsubmission` entity. When a form submission record is created, the
plugin checks whether a summary record already exists for the submitter
(matched by email). If it does, the existing record is updated with the latest
data and the submission count is incremented. Otherwise a new summary record is
created.

**Registration (Plugin Registration Tool):**

1. Build the plugin project (`dotnet build Plugins/`).
2. Register the assembly in Dataverse using the Plugin Registration Tool.
3. Add a step: **Message** = `Create`, **Entity** = `new_formsubmission`,
   **Stage** = Post-Operation.

### 2. JavaScript Web Resource (client-side)

**File:** `WebResources/formSubmissionHandler.js`

A client-side script for model-driven app forms. After the form's **OnSave**
event fires successfully, the handler uses `Xrm.WebApi` to query for an
existing summary record and either creates or updates one.

**Setup:**

1. Upload the JS file as a web resource in your solution.
2. Add it to the form's **Form Libraries**.
3. Register the **OnSave** event → `FormSubmission.onFormSave`.

### 3. Power Fx (Canvas App)

**File:** `PowerFx/FormSubmission.fx`

Power Fx formulas for a Canvas App. The `OnSuccess` property of a form control
uses `Patch()` to create or update a record in the **Submission Summaries**
table after the form is saved.

## Table Schema

Both the plugin and web resource expect the following tables:

### Form Submissions (`new_formsubmission`)

| Column             | Type     | Description                  |
|--------------------|----------|------------------------------|
| `new_email`        | Text     | Submitter email              |
| `new_fullname`     | Text     | Submitter full name          |
| `new_formtype`     | Text     | Type/name of the form        |
| `new_responsedata` | Text     | Serialized form response     |
| `new_processed`    | Boolean  | Whether submission was synced |
| `new_processedon`  | DateTime | When it was processed        |

### Submission Summaries (`new_submissionsummary`)

| Column                 | Type     | Description                  |
|------------------------|----------|------------------------------|
| `new_email`            | Text     | Submitter email (unique key) |
| `new_fullname`         | Text     | Latest submitter name        |
| `new_lastformtype`     | Text     | Most recent form type        |
| `new_lastresponsedata` | Text     | Most recent response data    |
| `new_submissioncount`  | Integer  | Total submissions count      |
| `new_lastsubmittedon`  | DateTime | Most recent submission time  |
