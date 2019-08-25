See the [original project](https://github.com/microsoft/vsts-work-item-migrator) for the base readme.

# My To Do list
* Create field mapping that can replace value with regex (Done)

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
    
* Migrate comments
    * Does comment migration happen?
    * Make identity mapper work here too
    
 * Create multiple work item history attachment formats
    * JSON is a poor choice for non-technical people (I assume it was the easiest... just upload the response from the api)
    * Text
    * XML?
    * Word?
