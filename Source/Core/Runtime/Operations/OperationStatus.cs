﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Coyote.Runtime
{
    /// <summary>
    /// The status of a controlled operation.
    /// </summary>
    internal enum OperationStatus
    {
        /// <summary>
        /// The operation does not have a status yet.
        /// </summary>
        None = 0,

        /// <summary>
        /// The operation is enabled.
        /// </summary>
        Enabled,

        /// <summary>
        /// The operation is waiting for all of its dependencies to complete.
        /// </summary>
        BlockedOnWaitAll,

        /// <summary>
        /// The operation is waiting for any of its dependencies to complete.
        /// </summary>
        BlockedOnWaitAny,

        /// <summary>
        /// The operation is waiting to receive an event.
        /// </summary>
        BlockedOnReceive,

        /// <summary>
        /// The operation is waiting to acquire a resource.
        /// </summary>
        BlockedOnResource,

        /// <summary>
        /// The operation is delayed.
        /// </summary>
        Delayed,

        /// <summary>
        /// The operation is completed.
        /// </summary>
        Completed
    }
}
