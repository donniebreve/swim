See the [original project](https://github.com/microsoft/vsts-work-item-migrator) for the base readme.

# My To Do list
* Create field mapping that can replace value with regex (Done)

* Clean up the configuration class (Done)
    * Create an interface
    
* Cleanup the WorkItemMigrationState classes (In Progress)
    * Seems like there are multiple details that can be combined into a single WorkItemMigration object
        * State
        * TargetID
        * RemoteLinks
        
* Fix the work item creation / update (Done)
    * Validation gathers the work items (make this clearer? move this out of validation?)
    * Updates do not trigger for field updates, only phase 2 processors?
    * skip-existing: false, to me, says update the target with the source, *i.e. overwrite*
        * maybe make this clearer (rename skip-existing)
        * make an overwrite operation

* Create an IdentityMapping option (In Progress, done but needs to be used in more places/fields)
    * IdentityMapper should run before work item creation / update
    * Look at the classes that exist for detecting invalid fields
    * Make something similar for fields with identities
    * Before work item create or update, replace identity fields with mapping
    
* Migrate comments (Done)
    * Does comment migration happen?
    * Make identity mapper work here too (In Progress)
    
 * Create multiple work item history / comment attachment formats (Done)
    * JSON is a poor choice for non-technical people (I assume it was the easiest... just upload the response from the api)
    * Text
    * Comment (!!!)
