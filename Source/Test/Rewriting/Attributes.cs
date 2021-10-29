﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Coyote.Rewriting
{
    /// <summary>
    /// Attribute that contains a signature identifying the parameters used during
    /// binary rewriting of an assembly.
    /// </summary>
    /// <remarks>
    /// If this attribute is applied to an assembly manifest, it denotes that the
    /// assembly has been rewritten.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class RewritingSignatureAttribute : Attribute
    {
        /// <summary>
        /// The version of Coyote used for the rewriting.
        /// </summary>
        public readonly string Version;

        /// <summary>
        /// Signature identifying parameters used during rewriting.
        /// </summary>
        public readonly string Signature;

        /// <summary>
        /// Initializes a new instance of the <see cref="RewritingSignatureAttribute"/> class.
        /// </summary>
        public RewritingSignatureAttribute(string version, string signature)
        {
            this.Version = version;
            this.Signature = signature;
        }
    }
}
