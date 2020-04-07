# Simple Work Item Migrator
_...to Azure DevOps_
This project was forked from the [original project](https://github.com/microsoft/vsts-work-item-migrator) by Microsoft. I ended up changing and simplifying much of the core code, so it would be difficult to try to merge anything back upstream.

# Added Features
* Field mapping that can replace values with regular expressions
* Identity mapper (very rough, just match and replace)
* Additional comment/history attachment formats: JSON/Text/HTML
* Migrates comments
* Migrates history

# In Progress
* Clean up the configuration class
   * Create an interface
* Cleanup the WorkItemMigrationState classes
    * Seems like there are multiple details that can be combined into a single WorkItemMigration object
        * State
        * TargetID
        * RemoteLinks
* Fix the work item creation / update
    * Validation gathers the work items (make this clearer? move this out of validation?)
    * Updates do not trigger for field updates, only phase 2 processors?
    * skip-existing: false, to me, says update the target with the source, *i.e. overwrite*
        * maybe make this clearer (rename skip-existing)
        * make an overwrite operation
* Create an IdentityMapping option
    * IdentityMapper should run before work item creation / update
    * Look at the classes that exist for detecting invalid fields
    * Make something similar for fields with identities
    * Before work item create or update, replace identity fields with mapping

# To Do
 * Additional formats for comments and history
 * Make identity mapper apply when migrating comments
 * Make identity mapper smarter, pull from project identity lists
 * Migrate iterations if they do not exist
 * Migrate area paths if they do not exist
 * Separate the validation and migration code and process
 * Better reporting about missing or unmapped items
