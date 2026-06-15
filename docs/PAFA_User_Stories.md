# PAFA Platform - User Stories

## 1. Scheduled Automation
**Description:**
As the PAFA platform, I want triggers automatically configured to accommodate file delivery times so that the process runs reliably without manual intervention and executes at the right time or in response to the right conditions.

**Acceptance Criteria:**
* **Scheduled Automation:**
  * Given an inbound file is available to be processed
  * And an auto schedule (cron job) has been configured to automatically check for new files
  * And the cron job runs every hour across a defined window around the expected delivery date (from the 16th to 20th of each month).
  * Then the file should be automatically validated and processed. 
* **Validation Block:**
  * Given an inbound file is submitted for processing
  * When the file fails one or more validation checks
  * Then the file should not be processed until all validation checks are met and errors corrected.
  * Once validation has re-occurred and passed then the file should be automatically processed.
* **Versioned Files:**
  * Given an inbound file fails one or more validation checks
  * When the file is resubmitted
  * Then a versioned filename must be used (for example, filename_v1, filename_v2)
  * And the system must reprocess the versioned file
  * And the previous failed version must remain unchanged
  * And the latest version must be treated as the active file
* **Expected Files:**
  * Given the system is expecting one or more files
  * When the system checks for file availability 
  * Then the files should be automatically validated and processed

---

## 2. Configure and Manage Manual Process Triggers
**Description:**
As the PAFA platform user, I want the ability to manually trigger the validation and processing workflow to accommodate for variable file delivery times and reprocessing after corrections, so that the process runs reliably.

**Acceptance Criteria:**
* **Manual triggers:**
  * Given an inbound file is available to be processed
  * And the time is outside the defined window to initiate automatic processing
  * Then the platform shall enable the user to manually trigger the validation and processing workflow
* **Expected File Automation:**
  * Given the system is expecting a file
  * Then it should automatically check the availability of said file 
  * And validate and process automatically.
* **Re-initiate after Corrections:**
  * Given an inbound file is submitted for processing
  * When the file fails one or more validation checks
  * Then a manual trigger shall be used to re-initiate validation and processing once corrections are made if required.

---

## 3. File Naming Convention and Folder Structure Requirements
**Description:**
As a PAFA user, I want all reporting files to follow a consistent naming convention that includes the month, and to be stored in a fixed Year/Month folder structure, so that automated processing can reliably locate, retrieve, and process the files without errors or manual intervention.

**Acceptance Criteria:**
* **File Naming Convention:**
  * The file name must include the reporting month in the agreed format (e.g. MMM or MM).
  * The file name must follow the standard naming convention, including:
    * A consistent prefix (e.g. report type or data category)
    * The reporting month
    * Versioning, following a consistent format
  * The file name must not contain prohibited or unsupported characters (e.g. *, /, ?, :).
  * The file name must be easily identifiable and readable for automated processing tools.
  * *Current file naming structures: Input file*
    * `MOD520A__PAF_Reports_Mar26_Non Anonymised`
    * `MOD520A__PAF_Reports_Mar26_Anonymised`
* **Folder Structure Requirements:**
  * Files must be stored under a fixed folder hierarchy structured as: `<Year>/<Month>`
  * The Year and Month folders must not be renamed, deleted, or reorganized.
  * Any files placed outside this structure must be flagged.
* **Automated Processing Compatibility:**
  * The automated processing system must be able to locate files based solely on the existing Year/Month folder structure.
  * The automated process must run successfully when files follow the naming convention and folder structure.
  * If the naming or folder structure deviates, the system must log an error.

---

## 4. Data Source Ingestion (ExoServe and DDP to SharePoint)
**Description:**
As a PAFA system user, I want the platform to reliably ingest and recognise data uploaded by ExoServe and DDP to SharePoint so that I can access all required operational data from the correct sources with the appropriate permissions.

**Acceptance Criteria:**
* **Data Source:**
  * AC1: The system must allow configuration of SharePoint as a primary data source.
* **File Location & Format Recognition:**
  * AC2: The system must correctly identify the file path/location of monthly SharePoint uploads organised by year and month.
  * AC3: The system must validate and recognise all supported file formats.
  * AC4: The system must raise an error or notification if a file is missing, inaccessible, or in an unexpected format.
  * AC5: The system must be able to identify if there are changes to an existing file and process.
  * AC6: File structure: shall follow the naming convention `PAFA/[YYYY]/[MM]/[Filename]`
* **Access & Credentials:**
  * AC7: The system must use dedicated access credentials for retrieving files from SharePoint.
  * AC8: The system must prevent data access if credentials are invalid or expired, and surface an appropriate error message.
  * AC9: Implement a retry mechanism rather than immediate failure. 3 retries with exponential backoff (e.g. wait 5 mins, then 15, then 30) before registering a definitive error.
  * AC10: After all retries fail, trigger the standard failure notification process, alerting the relevant users so they can investigate and take manual action.
* **Data Availability:**
  * AC11: The system must successfully read files uploaded to SharePoint when correct credentials are applied.

---

## 5. Downstream Processing & Quarantine
**Description:**
As the PAFA platform, I want to automatically quarantine any inbound file that fails validation and record the failure so that invalid data does not enter downstream processes and there is a clear audit trail for investigation and remediation.

**Acceptance Criteria:**
* **Validation Criteria:**
  * Given an inbound file is submitted for processing
  * And the file follows the validation rules, format and content
  * When the cron job runs
  * Then the file must be successfully processed
* **Handling Invalid Files:**
  * Given an inbound file is submitted for processing
  * And the file(s) fails one or more validation checks
  * When the cron job runs
  * Then the entire pipeline for generating reports and dashboards should continue processing (with the exception of the failed files) to all downstream processing stages.
  * Given an inbound file is submitted for processing
  * And the file fails one or more validation checks
  * When the cron job runs
  * Then the file must be moved to a designated quarantine location - A link to the specific folder, with read only permission should be provided in the notification message.
  * And it must not proceed to any downstream processing stage.
* **Failure Logging:**
  * Given a file fails validation
  * When the failure is detected
  * Then the system must create a log entry that includes: Timestamp of the failure, Unique correlation ID, File name, Validation rule(s) that failed.
* **Correlation ID Requirements:**
  * Given a failure event is logged
  * Then the correlation ID must uniquely identify that specific validation run
  * And must be retrievable for debugging, auditing, or cross‑system tracing.
* **Observability:**
  * Given a failure has been logged
  * When a user reviews system logs or monitoring dashboards
  * Then the failure event must be visible, searchable, and linked to the correlation ID.
* **No Partial Processing:**
  * Given a file fails validation
  * Then no partial writes, partial ingestion, or partial transformations should occur.

---

## 6. File Validation Failure Notification
**Description:**
As the PAFA platform user, I want the system to automatically send a failure notification only after all uploaded files have finished processing so that I receive a single, consolidated notification instead of multiple alerts during processing.

**Acceptance Criteria:**
* **Notification Trigger:**
  * Given multiple files have been submitted for processing
  * When one or more validation rules fail
  * Then the system must automatically generate and send a failure notification listing all files that failed.
* **Notification Content – Mandatory Fields:**
  * Given a validation failure occurs
  * When the notification is generated
  * Then the notification must include at minimum the following details:
    * File name
    * Reporting month
    * Data source: CDSP - SharePoint OR DDP - DDP files to be manually extracted by the team and saved in the same SharePoint location
    * Failure type (e.g. formatting error, missing mandatory field, invalid value) - all errors should be contained in 1 validation error message
    * First 10 row/field examples. 
* **Row/Field Examples:**
  * Given the system has identified rows or fields that triggered the validation failure
  * When preparing the notification
  * Then the system must extract the first 10 entries of the failed rule
  * And present them in a readable format (e.g., row number + field value(s) causing the error)
  * *Note:* A clean HTML table would be the best to record this: readable for business users but structured enough for clarity. Avoid JSON or raw text.
* **Notification Delivery:**
  * Given the notification is generated
  * When sending the details
  * Then it must be delivered to the predefined recipients (e.g. system users, data owners, or a DL).
  * An attached file showing all errors to be included without cluttering the email body. The email itself can contain a brief summary.
* **Error Clarity:**
  * Given a user receives the notification
  * When reviewing it
  * Then the user must be able to clearly understand: What failed, Why it failed, Which specific records illustrate the failure.
* **System Logging:**
  * Given a validation failure occurs
  * When the notification is sent
  * Then the system must log the failure event and notification dispatch for audit purposes, the log file must be accessible by the users.
* **Validation errors (Examples):**
  * **Change of File name:** There could be instances where the file name may be slightly different.
  * **Change of Table Name:** One of the table names could be slightly different than previous.
  * **Missing field:** One of the columns or data items could be missing.
  * **Change of Shippers:** A new Shipper may be added to a report or a Shipper may be removed.
  * **Invalid Value:** In previous years there were some instances where some percentages where over 100% which was incorrect.
  * **Hidden Columns:** There were previous instances where columns were hidden on a Excel spreadsheet.
