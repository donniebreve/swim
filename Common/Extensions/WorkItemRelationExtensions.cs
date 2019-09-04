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
    }
}
