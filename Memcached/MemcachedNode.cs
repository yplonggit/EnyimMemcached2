﻿using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Enyim.Caching.Memcached;
using System.Threading;
using Enyim.Caching.Memcached.Operations;

namespace Enyim.Caching.Memcached
{
	public class MemcachedNode : NodeBase
	{
		private const int SilentCountThreshold = 50;
		private static readonly ILog log = LogManager.GetCurrentClassLogger();

		private int silentCount;
		private bool lasWasSilent = false;

		public MemcachedNode(ICluster owner, IPEndPoint endpoint, IFailurePolicy failurePolicy, ISocket socket)
			: base(owner, endpoint, failurePolicy, socket) { }

		public override void Connect(bool reset, CancellationToken token)
		{
			silentCount = 0;

			base.Connect(reset, token);
		}

		protected override IResponse CreateResponse()
		{
			return new BinaryResponse();
		}

		public override Task<IOperation> Enqueue(IOperation op)
		{
			EnqueueNoOpIfNeeded(op);

			return base.Enqueue(op);
		}

		/// <summary>
		///  Add a NoOp after every 50th continous silent op
		/// </summary>
		/// <param name="op"></param>
		private void EnqueueNoOpIfNeeded(IOperation op)
		{
			if (IsSilent(op))
			{
				log.Trace("Got a silent op " + op + " count: " + silentCount);

				if (++silentCount < SilentCountThreshold)
					return;

				log.Trace("Got to threshold, injecting NoOp");

				base.Enqueue(new NoOp());
			}

			silentCount = 0;
		}

		private static bool IsSilent(IOperation op)
		{
			var silent = op as ICanBeSilent;

			return silent != null && silent.Silent;
		}

		protected override void WriteOp(Data data)
		{
			// remember of the last op was silent so that when we
			// run out of ops we'll know if we have to emit an additional NoOp
			// ***SSSSS<EOF>
			// * = normal
			// S = Silent
			// noop should be at <EOF> otherwise we won't get responses to the last
			// commands until we get a new op queued up
			lasWasSilent = IsSilent(data.Op);

			base.WriteOp(data);
		}

		protected override Data GetNextOp()
		{
			var data = base.GetNextOp();

			// we've temporarily ran out of commands
			if (data.IsEmpty && lasWasSilent)
				return new Data { Op = new NoOp() };

			return data;
		}
	}
}

#region [ License information          ]

/* ************************************************************
 *
 *    Copyright (c) Attila Kiskó, enyim.com
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion