using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Extensions
{
    public static class WorkItemExtensions
    {
        /// <summary>
        /// Finds the Attachment WorkItemRelation on this WorkItem matching the given WorkItemRelation.
        /// </summary>
        /// <param name="workItem">The work item.</param>
        /// <param name="workItemRelation">The source WorkItemRelation.</param>
        /// <returns>The matching WorkItemRelation, if found.</returns>
        public static WorkItemRelation FindAttachment(this WorkItem workItem, WorkItemRelation workItemRelation)
        {
            if (workItem.Relations == null)
            {
                return null;
            }
            foreach (WorkItemRelation relation in workItem.Relations)
            {
                if (relation.IsAttachment())
                {
                    // To do: I think this sould be name and resourceModifiedDate, but the correct modified date is not being sent when migrating
                    if (Object.Equals(workItemRelation.Attributes["name"], relation.Attributes["name"])
                        && Object.Equals(workItemRelation.Attributes["resourceSize"], relation.Attributes["resourceSize"]))
                    {
                        return relation;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the Attachment WorkItemRelation on this WorkItem matching the given file name and size.
        /// </summary>
        /// <param name="workItem">The work item.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="resourceSize">The resource size.</param>
        /// <returns>The matching WorkItemRelation, if found.</returns>
        public static WorkItemRelation FindAttachment(this WorkItem workItem, string fileName, long resourceSize)
        {
            if (workItem.Relations == null)
            {
                return null;
            }
            foreach (WorkItemRelation relation in workItem.Relations)
            {
                if (relation.IsAttachment())
                {
                    // To do: I think this sould be name and resourceModifiedDate, but the correct modified date is not being sent when migrating
                    if (Object.Equals(fileName, relation.Attributes["name"])
                        && Object.Equals(resourceSize, relation.Attributes["resourceSize"]))
                    {
                        return relation;
                    }
                }
            }
            return null;
        }
    }
}
