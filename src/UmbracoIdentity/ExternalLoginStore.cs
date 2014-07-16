using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.Linq;
using Microsoft.AspNet.Identity;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace UmbracoIdentity
{
    /// <summary>
    /// We are using a custom sqlce db to store this information
    /// </summary>
    internal class ExternalLoginStore : DisposableObject, IExternalLoginStore
    {
        //TODO: What is the OWIN form of MapPath??

        public static readonly object Locker = new object();
        private readonly UmbracoDatabase _db;

        private const string ConnString = @"Data Source=|DataDirectory|\UmbracoIdentity.sdf;Flush Interval=1;";

        public ExternalLoginStore()
        {
            if (!System.IO.File.Exists(IOHelper.MapPath("~/App_Data/UmbracoIdentity.sdf")))
            {
                using (var en = new SqlCeEngine(ConnString))
                {
                    en.CreateDatabase();
                }    
            }
            _db = new UmbracoDatabase(ConnString, "System.Data.SqlServerCe.4.0");
            if (!_db.TableExist("ExternalIdentities"))
            {
                _db.CreateTable<ExternalLoginDto>();
            }
        }

        public int? Find(UserLoginInfo login)
        {
            var sql = new Sql() 
                .Select("*")
                .From<ExternalLoginDto>()
                .Where<ExternalLoginDto>(dto => dto.LoginProvider == login.LoginProvider && dto.ProviderKey == login.ProviderKey);

            var found = _db.Fetch<ExternalLoginDto>(sql);

            return found.Any()
                ? found.First().UserId
                : (int?) null;
        }

        public void SaveUserLogins(int memberId, IEnumerable<UserLoginInfo> logins)
        {
            using (var t = _db.GetTransaction())
            {
                //clear out logins for member
                _db.Execute("DELETE FROM ExternalLogins WHERE UserId=@userId", new {userId = memberId});

                //add them all
                foreach (var l in logins)
                {
                    _db.Insert(new ExternalLoginDto
                    {
                        LoginProvider = l.LoginProvider,
                        ProviderKey = l.ProviderKey,
                        UserId = memberId
                    });
                }

                t.Complete();
            }
        }

        protected override void DisposeResources()
        {
            _db.Dispose();
        }

        [TableName("ExternalLogins")]
        [ExplicitColumns]
        [PrimaryKey("ExternalLoginId")]
        internal class ExternalLoginDto
        {
            [Column("ExternalLoginId")]
            [PrimaryKeyColumn(Name = "PK_ExternalLoginId")]
            public int ExternalLoginId { get; set; }

            [Column("UserId")]
            public int UserId { get; set; }

            [Column("LoginProvider")]
            [Length(4000)]
            [NullSetting(NullSetting = NullSettings.NotNull)]
            public string LoginProvider { get; set; }

            [Column("ProviderKey")]
            [Length(4000)]
            [NullSetting(NullSetting = NullSettings.NotNull)]
            public string ProviderKey { get; set; }
        }
    }
}