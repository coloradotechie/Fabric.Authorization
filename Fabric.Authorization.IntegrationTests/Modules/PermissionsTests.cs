﻿using System.Collections.Generic;
using System.Security.Claims;
using Fabric.Authorization.API.Constants;
using Fabric.Authorization.API.Modules;
using Fabric.Authorization.Domain.Services;
using Fabric.Authorization.Domain.Stores;
using Moq;
using Nancy;
using Nancy.Testing;
using Serilog;
using Xunit;

namespace Fabric.Authorization.IntegrationTests
{
    public class PermissionsTests : IntegrationTestsFixture
    {
        public PermissionsTests(bool useInMemoryDB = true)
        {
            var store = useInMemoryDB ? new InMemoryPermissionStore() : (IPermissionStore)new CouchDBPermissionStore(this.DbService(), this.Logger);
            var clientStore = useInMemoryDB ? new InMemoryClientStore() : (IClientStore)new CouchDBClientStore(this.DbService(), this.Logger);

            var permissionService = new PermissionService(store);
            var clientService = new ClientService(clientStore);

            this.Browser = new Browser(with =>
            {
                with.Module(new PermissionsModule(
                        permissionService,
                        clientService,
                        new Domain.Validators.PermissionValidator(store),
                        this.Logger));
                with.Module(new ClientsModule(clientService,
                        new Domain.Validators.ClientValidator(clientStore),
                        this.Logger));
                with.RequestStartup((_, __, context) =>
                {
                    context.CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>()
                    {
                        new Claim(Claims.Scope, Scopes.ManageClientsScope),
                        new Claim(Claims.Scope, Scopes.ReadScope),
                        new Claim(Claims.Scope, Scopes.WriteScope),
                        new Claim(Claims.ClientId, "permissionprincipal"),
                    }, "permissionprincipal"));
                });
            });

            this.Browser.Post("/clients", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", "permissionprincipal");
                with.FormValue("Name", "permissionprincipal");
                with.Header("Accept", "application/json");
            }).Wait();
        }

        [Theory]
        [InlineData("InexistentPermission")]
        [InlineData("InexistentPermission2")]
        public void TestGetPermission_Fail(string permission)
        {
            var get = this.Browser.Get($"/permissions/app/permissionprincipal/{permission}", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode); //TODO: Should be OK or NotFound?
            Assert.True(!get.Body.AsString().Contains(permission));
        }

        [Theory]
        [InlineData("Perm1")]
        [InlineData("Perm2")]
        public void TestAddNewPermission_Success(string permission)
        {
            var postResponse = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "permissionprincipal");
                with.FormValue("Name", permission);
            }).Result;

            var getResponse = this.Browser.Get($"/permissions/app/permissionprincipal/{permission}", with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                }).Result;

            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.True(getResponse.Body.AsString().Contains(permission));
        }

        [Theory]
        [InlineData("RepeatedPermission1")]
        [InlineData("RepeatedPermission2")]
        public void TestAddNewPermission_Fail(string permission)
        {
            this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "permissionprincipal");
                with.FormValue("Name", permission);
            }).Wait();

            // Repeat
            var postResponse = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "permissionprincipal");
                with.FormValue("Name", permission);
            }).Result;

            Assert.Equal(HttpStatusCode.BadRequest, postResponse.StatusCode);
        }

        [Theory]
        [InlineData("PermissionToBeDeleted")]
        [InlineData("PermissionToBeDeleted2")]
        public void TestDeletePermission_Success(string permission)
        {
            this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", "18F06565-AAAA-BBBB-AF27-CEFC165B20FA");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "permissionprincipal");
                with.FormValue("Name", permission);
            }).Wait();

            var delete = this.Browser.Delete("/permissions/18F06565-AAAA-BBBB-AF27-CEFC165B20FA", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }

        [Theory]
        [InlineData("18F06565-AA9E-4315-AF27-CEFC165B20FA")]
        [InlineData("18F06565-AA9E-4315-AF27-CEFC165B20FB")]
        public void TestDeletePermission_Fail(string permission)
        {
            var delete = this.Browser.Delete($"/permissions/{permission}", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }
    }
}