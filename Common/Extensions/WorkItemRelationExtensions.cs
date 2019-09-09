using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;

namespace Common.Extensions
{
    public static class WorkItemRelationExtensions
    {
        /// <summary>
        /// Checks if the given WorkItemRelation is an attachment.
        /// </summary>
        /// <param name="relation">The WorkItemRelation.</param>
        /// <returns>True if the relation is an attachment.</returns>
        public static bool IsAttachment(this WorkItemRelation relation)
        {
            return relation.Rel.Equals(Constants.AttachedFile, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the given WorkItemRelation is a hyperlink.
        /// </summary>
        /// <param name="relation">The WorkItemRelation.</param>
        /// <returns>True if the relation is an hyperlink.</returns>
        public static bool IsHyperlink(this WorkItemRelation relation)
        {
            return relation.Rel.Equals(Constants.Hyperlink, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a nice relation name for this relation.
        /// </summary>
        /// <param name="relation">The WorkItemRelation.</param>
        /// <returns>A nice name for the relation.</returns>
        public static string GetRelationName(this WorkItemRelation relation)
        {
            switch (relation.Rel)
            {
                case "Microsoft.VSTS.Common.Affects-Forward":
                    return "Affects";
                case "Microsoft.VSTS.Common.Affects-Reverse":
                    return "Affected By";
                case "System.LinkTypes.Hierarchy-Forward":
                    return "Child";
                case "System.LinkTypes.Hierarchy-Reverse":
                    return "Parent";
                case "System.LinkTypes.Duplicate-Forward":
                    return "Duplicate";
                case "System.LinkTypes.Duplicate-Reverse":
                    return "Duplicate Of";
                case "Microsoft.VSTS.TestCase.SharedParameterReferencedBy":
                    return "References";
                case "System.LinkTypes.Related":
                    return "Related";
                case "System.LinkTypes.Dependency":
                    return "Depends On";
                case "Microsoft.VSTS.Common.TestedBy-Forward":
                    return "Tested By";
                case "Microsoft.VSTS.Common.TestedBy-Reverse":
                    return "Tests";
                case "Microsoft.VSTS.TestCase.SharedStepReferencedBy":
                    return "Shared Step";
                default:
                    return relation.Rel;
            }
        }
    }
}
