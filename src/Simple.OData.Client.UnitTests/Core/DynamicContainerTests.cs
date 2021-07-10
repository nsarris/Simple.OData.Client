﻿using Simple.OData.Client.Tests.Entities;
using Xunit;

namespace Simple.OData.Client.Tests.Core
{
    public class DynamicContainerTests
    {
        [Fact]
        public void ContainerName()
        {
            var typeCache = new TypeCache();

            typeCache.Register<Animal>();

            Assert.Equal("DynamicProperties", typeCache.DynamicContainerName(typeof(Animal)));
        }

        [Fact]
        public void ExplicitContainerName()
        {
            var typeCache = new TypeCache();

            typeCache.Register<Animal>("Foo");

            Assert.Equal("Foo", typeCache.DynamicContainerName(typeof(Animal)));
        }

        [Fact]
        public void SubTypeContainerName()
        {
            var typeCache = new TypeCache();

            typeCache.Register<Animal>();

            Assert.Equal("DynamicProperties", typeCache.DynamicContainerName(typeof(Mammal)));
        }
    }
}