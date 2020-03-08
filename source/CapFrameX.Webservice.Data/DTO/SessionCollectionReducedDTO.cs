﻿using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class SessionCollectionReducedDTO
	{
		public Guid Id { get; set; }
		public Guid? UserId { get; set; }
		public DateTime Timestamp { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public virtual IEnumerable<ISessionInfo> Sessions { get; set; }
	}
}