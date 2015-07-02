﻿using App.Models;
using App.Utils.Excel;
using OfficeOpenXml;
using Scaffold;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace App.Controllers.Models
{
    public class DocumentUploadController: BaseController<DocumentUpload, long>
    {
        private IDictionary<DocumentUploadType, IDocumentUploadAdapter> adapters = new Dictionary<DocumentUploadType, IDocumentUploadAdapter>();
        public DocumentUploadController(DB dbContext)
            : base(dbContext)
        {
            Include(d => d.Organization);
            Include(d => d.CreatedBy);
            Include(d => d.Region);
            adapters[DocumentUploadType.NationalDd] = new NationalDocumentUploadAdapter<NationalDdAllocation>() 
            { 
                DbContext = dbContext,
                Init = (context, allocation) =>
                {
                    allocation.fkApbnId = context.Apbn.Id;
                },
            };
            adapters[DocumentUploadType.RegionalDd] = new NationalDocumentUploadAdapter<RegionalDdAllocation>() 
            { 
                DbContext = dbContext,
                Init = (context, allocation) =>
                {
                    allocation.fkApbdId = dbContext.Set<Apbd>().FirstOrDefault(a => a.fkApbnId == context.Apbn.Id && a.fkRegionId == context.Region.Id).Id;
                },
            };
            adapters[DocumentUploadType.NationalAdd] = new NationalDocumentUploadAdapter<NationalAddAllocation>() 
            { 
                DbContext = dbContext,
                Init = (context, allocation) =>
                {
                    allocation.fkApbdId = dbContext.Set<Apbd>().FirstOrDefault(a => a.fkApbnId == context.Apbn.Id && a.fkRegionId == allocation.fkRegionId).Id;
                },
            };
            adapters[DocumentUploadType.RegionalAdd] = new NationalDocumentUploadAdapter<RegionalAddAllocation>() 
            { 
                DbContext = dbContext,
                Init = (context, allocation) =>
                {
                    allocation.fkApbdId = dbContext.Set<Apbd>().FirstOrDefault(a => a.fkApbnId == context.Apbn.Id && a.fkRegionId == context.Region.Id).Id;
                },
            };
            adapters[DocumentUploadType.NationalBhpr] = new NationalDocumentUploadAdapter<NationalBhprAllocation>() 
            { 
                DbContext = dbContext,
                Init = (context, allocation) =>
                {
                    allocation.fkApbdId = dbContext.Set<Apbd>().FirstOrDefault(a => a.fkApbnId == context.Apbn.Id && a.fkRegionId == allocation.fkRegionId).Id;
                },
            };
            adapters[DocumentUploadType.RegionalBhpr] = new NationalDocumentUploadAdapter<RegionalBhprAllocation>() 
            { 
                DbContext = dbContext,
                Init = (context, allocation) =>
                {
                    allocation.fkApbdId = dbContext.Set<Apbd>().FirstOrDefault(a => a.fkApbnId == context.Apbn.Id && a.fkRegionId == context.Region.Id).Id;
                },
            };
        }
        protected override IQueryable<DocumentUpload> ApplyQuery(IQueryable<DocumentUpload> query)
        {
            var orgId = GetQueryString<long?>("fkOrganizationId");
            if(orgId.HasValue) 
                query = query.Where(r => r.fkOrganizationId == orgId.Value);

            var type = GetQueryString<int?>("Type");
            if(type.HasValue) 
                query = query.Where(r => r.Type == (DocumentUploadType) type);

            var apbnKey = GetQueryString<string>("ApbnKey");
            if (!string.IsNullOrEmpty(apbnKey))
                query = query.Where(r => r.ApbnKey == apbnKey);

            var regionId = GetQueryString<string>("fkRegionId");
            if (!string.IsNullOrEmpty(regionId))
                query = query.Where(r => r.fkRegionId == regionId);

            var createdById = GetQueryString<string>("fkCreatedById");
            if (!string.IsNullOrEmpty(createdById))
                query = query.Where(r => r.fkCreatedById == createdById);

            query = query.OrderByDescending(o => o.DateCreated).Take(20);

            return query;
        }

        [HttpGet]
        public DocumentUpload GetActive(int type, string regionId, string apbnKey)
        {
            return dbSet
                .Include(e => e.CreatedBy)
                .Include(e => e.Organization)
                .FirstOrDefault(d => d.Type == (DocumentUploadType)type && d.fkRegionId == regionId && d.ApbnKey == apbnKey && d.IsActivated);
        }

        [HttpGet]
        public HttpResponseMessage GetTemplate(int type, string regionId, string apbnKey)
        {
            DocumentUploadType t = (DocumentUploadType)type;
            var context = new AdapterContext(dbContext, t, regionId, apbnKey);
            return adapters[t].GetTemplate(context);
        }

        [HttpPost]
        [Authorize(Roles=Role.VOLUNTEER_ALLOCATION)]
        public void Upload(Multipart<DocumentUpload> multipart, int type, string regionId, string apbnKey)
        {
            KawalDesaController.CheckRegionAllowed(dbContext, regionId);
            DocumentUploadType t = (DocumentUploadType)type;
            var context = new AdapterContext(dbContext, t, regionId, apbnKey);
            if (context.Region.Type != RegionType.NASIONAL && context.Region.Type != RegionType.KABUPATEN)
                throw new ApplicationException("only allowed to upload on nasional or kabupaten");

            using (var tx = dbContext.Database.BeginTransaction())
            {
                adapters[t].Upload(multipart, context);
                tx.Commit();
            }
        }

        [HttpGet]
        [Authorize(Roles=Role.ADMIN)]
        public void GenerateDanaDesaKabs(string apbnKey)
        {
            if (!HttpContext.Current.IsDebuggingEnabled)
                throw new ApplicationException("this is a debug only feature");

            GenDanaDesaKab(dbContext, apbnKey);
        }

        public static void GenDanaDesaKab(DbContext dbContext, String apbnKey)
        {
            var kabs = dbContext.Set<Region>().Where(r => r.Type == RegionType.KABUPATEN).ToList();
            var existing = dbContext.Set<DocumentUpload>().Where(r => r.Type == DocumentUploadType.RegionalDd && r.ApbnKey == apbnKey && r.IsActivated);
            var existingIds = new HashSet<string>(existing.Select(e => e.fkRegionId));
            kabs = kabs.Where(k => !existingIds.Contains(k.Id)).ToList();
            var apbn = dbContext.Set<Apbn>().First(a => a.Key == apbnKey);
            int i = 0;
            foreach(var kab in kabs)
            {
                String log = String.Format("kab {0} - {1} of {2}", kab.Name, i, kabs.Count);
                File.AppendAllLines("D:\\Work\\kawaldesa.log", new String[]{log});
                var apbd = dbContext.Set<Apbd>().First(a => a.fkApbnId == apbn.Id && a.fkRegionId == kab.Id);
                var nationalAlloc = dbContext.Set<NationalDdAllocation>().FirstOrDefault(r => r.IsActivated && r.fkRegionId == kab.Id && r.fkApbnId == apbn.Id);
                if(nationalAlloc != null)
                {
                    var nationalDoc = dbContext.Set<DocumentUpload>().First(d => d.Id == nationalAlloc.fkDocumentUploadId);
                    GenDanaDesaKab(dbContext, apbd, kab, nationalAlloc, nationalDoc);
                }
                i++;
            }
        }


        private static void GenDanaDesaKab(DbContext dbContext, Apbd apbd, Region region, NationalDdAllocation nationalAlloc, DocumentUpload nationalDoc)
        {
            var regions = dbContext.Set<Region>().Where(r => r.Type == RegionType.DESA && !r.IsKelurahan && r.Parent.fkParentId == region.Id).ToList();
            var parentRegions = dbContext.Set<Region>().Where(r => r.Type == RegionType.KECAMATAN && r.fkParentId == region.Id).ToList();

            if(regions.Count == 0)
                return;

            decimal total = nationalAlloc.Dd ?? 0;
            decimal amountPerDes = total * 9 / (regions.Count * 10);
            var regionAllocs = new List<RegionalDdAllocation>();
            foreach(var child in regions)
            {
                var regionAlloc = new RegionalDdAllocation();
                regionAlloc.fkApbdId = apbd.Id;
                regionAlloc.fkRegionId = child.Id;
                regionAlloc.No = child.Id;
                regionAlloc.RegionName = child.Name;
                regionAlloc.BaseAllocation = amountPerDes;
                regionAlloc.Dd = amountPerDes;
                regionAllocs.Add(regionAlloc);
            }
            var fileBytes = new AllocationExcelWriter<RegionalDdAllocation>().WriteToBytes(parentRegions, regions, regionAllocs);

            var blob = new Blob();
            blob.Name = "Alokasi.xlsx";
            dbContext.Set<Blob>().Add(blob);
            dbContext.SaveChanges();
            File.WriteAllBytes(blob.FilePath, fileBytes);

            DocumentUpload upload = new DocumentUpload();
            upload.fkCreatedById = nationalDoc.fkCreatedById;
            upload.fkOrganizationId = nationalDoc.fkOrganizationId;
            upload.DateCreated = DateTime.Now;
            upload.DateActivated = DateTime.Now;
            upload.Type = DocumentUploadType.RegionalDd;
            upload.ApbnKey = nationalDoc.ApbnKey;
            upload.fkRegionId = region.Id;
            upload.Source = nationalDoc.Source;
            upload.Notes = "Alokasi Dasar 90%";
            upload.DocumentName = "Alokasi Dasar 90% APBN-P 2015";
            upload.FileName = blob.RelativeFileName;
            upload.fkFileId = blob.Id;
            dbContext.Set<DocumentUpload>().Add(upload);
            dbContext.SaveChanges();


            foreach(var regionAlloc in regionAllocs)
            {
                regionAlloc.fkDocumentUploadId = upload.Id;
                dbContext.Set<RegionalDdAllocation>().Add(regionAlloc);
            }
            dbContext.SaveChanges();

            new DocumentUploadActivator<RegionalDdAllocation>().Activate(dbContext, upload);
        }

        public interface IDocumentUploadAdapter
        {
            HttpResponseMessage GetTemplate(AdapterContext context);
            void Upload(Multipart<DocumentUpload> multipart, AdapterContext context);
        }

        public class AdapterContext
        {
            public Region Region { get; set; }
            public Apbn Apbn { get; set; }
            public DocumentUploadType Type { get; set; }

            public AdapterContext(DbContext dbContext, DocumentUploadType type, string regionId, string apbnKey)
            {
                this.Type = type;
                this.Region = dbContext.Set<Region>()
                    .Include(r => r.Parent)
                    .Include(r => r.Parent.Parent)
                    .Include(r => r.Parent.Parent.Parent)
                    .Include(r => r.Parent.Parent.Parent.Parent)
                    .First(r => r.Id == regionId);
                this.Apbn = dbContext.Set<Apbn>().First(a => a.Key == apbnKey);
            }
        }

        public class NationalDocumentUploadAdapter<TAllocation>: IDocumentUploadAdapter where TAllocation: class, IAllocation, new()
        {
            public DbContext DbContext { get; set; }
            public Action<AdapterContext, TAllocation> Init { get; set; }

            public HttpResponseMessage GetTemplate(AdapterContext context)
            {
                List<Region> regions = null;
                List<Region> parentRegions = null;
                if(context.Region.Type == RegionType.NASIONAL)
                {
                    regions = DbContext.Set<Region>().Where(r => r.Type == RegionType.KABUPATEN).ToList();
                    parentRegions = DbContext.Set<Region>().Where(r => r.Type == RegionType.PROPINSI).ToList();
                }
                else
                {
                    regions = DbContext.Set<Region>().Where(r => r.Type == RegionType.DESA && r.Parent.fkParentId == context.Region.Id).ToList();
                    parentRegions = DbContext.Set<Region>().Where(r => r.Type == RegionType.KECAMATAN && r.fkParentId == context.Region.Id).ToList();
                }
                var allocations = DbContext.Set<TAllocation>().Where(r => r.IsActivated).ToList();
                return new AllocationExcelWriter<TAllocation>().Write(parentRegions, regions, allocations);
            }

            public void Upload(Multipart<DocumentUpload> multipart, AdapterContext context)
            {
                try
                {
                    List<Region> regions = null;
                    if(context.Region.Type == RegionType.NASIONAL)
                    {
                        regions = DbContext.Set<Region>().Where(r => r.Type == RegionType.KABUPATEN).ToList();
                    }
                    else
                    {
                        regions = DbContext.Set<Region>().Where(r => r.Type == RegionType.DESA && r.Parent.fkParentId == context.Region.Id).ToList();
                    }
                    var allocations = new AllocationExcelReader<TAllocation>().Read(regions, new FileInfo(multipart.Files[0].FilePath));
                    var upload = multipart.Entity;
                    var user = KawalDesaController.GetCurrentUser();

                    var fileResult = multipart.Files[0];
                    var blob = new Blob(fileResult);
                    DbContext.Set<Blob>().Add(blob);
                    DbContext.SaveChanges();
                    fileResult.Move(blob.FilePath);

                    upload.FileName = blob.RelativeFileName;
                    upload.fkCreatedById = user.Id;
                    upload.fkOrganizationId = user.fkOrganizationId.Value;
                    upload.DateCreated = DateTime.Now;
                    upload.DateActivated = DateTime.Now;
                    upload.Type = context.Type;
                    upload.ApbnKey = context.Apbn.Key;
                    upload.fkRegionId = context.Region.Id;
                    upload.fkFileId = blob.Id;
                    DbContext.Set<DocumentUpload>().Add(upload);
                    DbContext.SaveChanges();

                    foreach(var allocation in allocations)
                    {
                        allocation.fkDocumentUploadId = upload.Id;
                        Init(context, allocation);
                        DbContext.Set<TAllocation>().Add(allocation);
                    }
                    DbContext.SaveChanges();

                    new DocumentUploadActivator<TAllocation>().Activate(DbContext, upload);
                }
                finally
                {
                    multipart.DeleteUnmoved();
                }
            }
        }
    }
}