﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Coyote.Runtime;

namespace Microsoft.Coyote.Testing.Fuzzing
{
    /// <summary>
    /// Abstract exploration strategy used during systematic fuzzing.
    /// </summary>
    internal abstract class FuzzingStrategy : Strategy
    {
        /// <summary>
        /// Provides access to the operation id associated with each asynchronous control flow.
        /// </summary>
        private static readonly AsyncLocal<Guid> OperationId = new AsyncLocal<Guid>();

        /// <summary>
        /// Map from task ids to operation ids.
        /// </summary>
        private readonly ConcurrentDictionary<int, Guid> OperationIdMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuzzingStrategy"/> class.
        /// </summary>
        internal FuzzingStrategy(Configuration configuration, bool isFair)
            : base(configuration, isFair)
        {
            this.OperationIdMap = new ConcurrentDictionary<int, Guid>();
        }

        /// <summary>
        /// Returns the next delay.
        /// </summary>
        /// <param name="ops">Operations executing during the current test iteration.</param>
        /// <param name="current">The operation requesting the delay.</param>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next delay.</param>
        /// <returns>True if there is a next delay, else false.</returns>
        internal bool GetNextDelay(IEnumerable<ControlledOperation> ops, ControlledOperation current,
            int maxValue, out int next) => this.NextDelay(ops, current, maxValue, out next);

        /// <summary>
        /// Returns the next delay.
        /// </summary>
        /// <param name="ops">Operations executing during the current test iteration.</param>
        /// <param name="current">The operation requesting the delay.</param>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next delay.</param>
        /// <returns>True if there is a next delay, else false.</returns>
        internal abstract bool NextDelay(IEnumerable<ControlledOperation> ops, ControlledOperation current,
            int maxValue, out int next);

        /// <summary>
        /// Returns the current operation id.
        /// </summary>
        protected Guid GetOperationId()
        {
            Guid id;
            if (Task.CurrentId is null)
            {
                id = OperationId.Value;
                if (id == Guid.Empty)
                {
                    id = Guid.NewGuid();
                    OperationId.Value = id;
                }
            }
            else
            {
                id = this.OperationIdMap.GetOrAdd(Task.CurrentId.Value, Guid.NewGuid());
                OperationId.Value = id;
            }

            return id;
        }
    }
}
