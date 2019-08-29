using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Migration
{
    public enum MigrationAction
    {
        /// <summary>
        /// Do nothing.
        /// </summary>
        None,

        /// <summary>
        /// Create the work item in the target.
        /// </summary>
        Create,

        /// <summary>
        /// Update the work item in the target.
        /// </summary>
        Update
    }
}
