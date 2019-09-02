using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Extensions
{
    public static class WiqlExtensions
    {
        /// <summary>
        /// Adds a where constraint.
        /// </summary>
        /// <param name="wiql">This Wiql.</param>
        /// <param name="constraint">The where constraint.</param>
        /// <returns>A new Wiql.</returns>
        public static Wiql AddWhereConstraint(this Wiql wiql, string constraint)
        {
            // where and orderby missing
                // add " where constraint"
            // where exists, orderby missing
                // add " and constraint"
            // where missing, orderby exists
                // add " where constraint " + orderby
            // where exists, orderby exists
                // add " and constraint " + orderby

            string query = wiql.Query;

            // Remove the order by clause
            string orderBy = "";
            int orderByIndex = query.LastIndexOf(" order by ", StringComparison.OrdinalIgnoreCase);
            if (orderByIndex > 0)
            {
                orderBy = query.Substring(orderByIndex);
                query = query.Substring(0, orderByIndex);
            }
            
            // Add the where constraint
            int whereIndex = query.LastIndexOf(" where ", StringComparison.OrdinalIgnoreCase);
            if (whereIndex < 0)
            {
                query = query + " where " + constraint;
            }
            if (whereIndex >= 0)
            {
                query = query.Insert(whereIndex + 7, "(") + ") and " + constraint;
            }

            // Add the order by clause
            query += orderBy;

            return new Wiql()
            {
                Query = query
            };
        }

        /// <summary>
        /// Replaces the order by clause with the given clause.
        /// </summary>
        /// <param name="wiql">This Wiql.</param>
        /// <param name="orderBy">The new order by clause.</param>
        /// <returns>A new Wiql.</returns>
        public static Wiql SetOrderBy(this Wiql wiql, string orderBy)
        {
            wiql = wiql.RemoveOrderBy();
            return new Wiql()
            {
                Query = $"{wiql.Query} order by {orderBy}"
            };
        }

        /// <summary>
        /// Removes the order by clause.
        /// </summary>
        /// <param name="wiql">This Wiql.</param>
        /// <returns>A new Wiql.</returns>
        public static Wiql RemoveOrderBy(this Wiql wiql)
        {
            int index = wiql.Query.LastIndexOf(" order by ", StringComparison.OrdinalIgnoreCase);
            return new Wiql()
            {
                Query = wiql.Query.Substring(0, index > 0 ? index : wiql.Query.Length)
            };
        }

        //public static Wiql Exclude(string postMoveTag)
        //{

        //}


        //public static string ParseQueryForPaging(string query, string postMoveTag)
        //{
        //    var lastOrderByIndex = query.LastIndexOf(" ORDER BY ", StringComparison.OrdinalIgnoreCase);
        //    var queryWithNoOrderByClause = query.Substring(0, lastOrderByIndex > 0 ? lastOrderByIndex : query.Length);

        //    if (!string.IsNullOrEmpty(postMoveTag))
        //    {
        //        var postMoveTagClause = !string.IsNullOrEmpty(postMoveTag) ? $"System.Tags NOT CONTAINS '{postMoveTag}'" : string.Empty;
        //        return $"{InjectWhereClause(queryWithNoOrderByClause, postMoveTagClause)}";
        //    }
        //    else
        //    {
        //        return queryWithNoOrderByClause;
        //    }
        //}

        //public static Wiql MakePageable(this Wiql wiql, int watermark, int id)
        //{
        //    string query = wiql.Query;
        //    wiql = wiql.AddWhereConstraint(
        //        $"AND ((System.Watermark > {watermark}) OR (System.Watermark = {watermark} AND System.Id > {id}))");

            
        //    query = $"{query}{additionalConstraint} ORDER BY System.Watermark, System.Id";
        //    return new Wiql()
        //    {
        //        Query = query
        //    };
        //}
    }
}
