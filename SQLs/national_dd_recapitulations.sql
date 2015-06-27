﻿create view national_dd_recapitulations as
select
  apbn.key || '-' || r.id as id,
  r.id as region_id,
  apbn.key as apbn_key,
  r.name as region_name,
  r.parent_id as parent_region_id,
  dd.regional_transfer as regional_transfer,
  dd.dd as dd,
  rdc.desa_count as total_desa,
  rdc.desa_count as completed_desa

  from apbns apbn
  cross join region_parents r
  inner join region_desa_counts rdc on r.id = rdc.id
  left outer join national_dd_allocations dd 
	on dd.is_activated = true
	and apbn.id = dd.fk_apbn_id 
	and r.id = dd.fk_region_id;