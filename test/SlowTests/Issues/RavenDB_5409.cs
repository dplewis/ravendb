﻿using System;
using System.Linq;
using System.Threading;
using FastTests;
using FastTests.Server.Basic.Entities;
using Raven.NewClient.Client.Indexes;
using Raven.NewClient.Data.Indexes;
using Raven.NewClient.Operations.Databases;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_5409 : RavenNewTestBase
    {
        private class Users_ByName : AbstractIndexCreationTask<User>
        {
            public Users_ByName()
            {
                Map = users => from u in users
                               select new
                               {
                                   u.Name
                               };

                Analyzers.Add(x => x.Name, "NotExistingAnalyzer");
            }
        }

        [Fact]
        public void AnalyzerErrorsShouldMarkIndexAsErroredImmediately()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    for (var i = 0; i < 1; i++)
                    {
                        session.Store(new User());
                    }

                    session.SaveChanges();
                }

                new Users_ByName().Execute(store);

                var result = SpinWait.SpinUntil(() => store.Admin.Send(new GetStatisticsOperation()).Indexes[0].State == IndexState.Error, TimeSpan.FromSeconds(5));

                Assert.True(result, "Index did not become errored.");
            }
        }
    }
}