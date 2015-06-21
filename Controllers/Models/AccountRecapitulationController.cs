﻿using App.Models;
using Scaffold;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace App.Controllers.Models
{
    public class BaseAccountRecapitulationController<TRecapitulation> : ReadOnlyController<TRecapitulation, long>
        where TRecapitulation: BaseAccountRecapitulation, new()
    {
        public BaseAccountRecapitulationController(DB dbContext)
            : base(dbContext)
        {
        }

        protected override IQueryable<TRecapitulation> ApplyQuery(IQueryable<TRecapitulation> query)
        {
            var parentID = GetQueryString<string>("ParentID");
            return query.Where(t => t.ParentRegionId == parentID || t.RegionId == parentID);
        }
    }
    public class AccountRecapitulationController : BaseAccountRecapitulationController<AccountRecapitulation>
    {
        public AccountRecapitulationController(DB dbContext)
            : base(dbContext)
        {
        }
    }
    public class FrozenAccountRecapitulationController : BaseAccountRecapitulationController<FrozenAccountRecapitulation>
    {
        public FrozenAccountRecapitulationController(DB dbContext)
            : base(dbContext)
        {
        }
    }
}