using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Xunit;
namespace MetadataClient
{
    public class MetadataJobFacts
    {
        [Fact]
        public void PackageAssertionForDeletePackage()
        {
            // Arrange
            var packageAssertions = new List<PackageAssertion>();
            var packageOwnerAssertions = new List<PackageOwnerAssertion>();
            packageAssertions.Add(new PackageAssertion("A", "1.0.0", false));
            var expectedAssertionsArray = JArray.Parse(@"[
  {
    'packageId': 'A',
    'version': '1.0.0',
    'exists': false
  }
]");

            // Act
            var actualAssertionsArray = MetadataJob.GetJArrayAssertions(packageAssertions, packageOwnerAssertions);
            Assert.Equal(expectedAssertionsArray, actualAssertionsArray);
        }

        [Fact]
        public void OwnerAssertionsOnly()
        {
            // Arrange
            var packageAssertions = new List<PackageAssertion>();
            var packageOwnerAssertions = new List<PackageOwnerAssertion>();
            packageOwnerAssertions.Add(new PackageOwnerAssertion("A", "1.0.0", "user1", true));
            packageOwnerAssertions.Add(new PackageOwnerAssertion("A", "1.0.0", "user2", false));
            var expectedAssertionsArray = JArray.Parse(@"[
  {
    'packageId': 'A',
    'version': '1.0.0',
    'exists': true,
    'owners': [
      {
        'username': 'user1',
        'exists': true
      },
      {
        'username': 'user2',
        'exists': false
      }
    ]
  }
]");
            // Act
            var actualAssertionsArray = MetadataJob.GetJArrayAssertions(packageAssertions, packageOwnerAssertions);
            Assert.Equal(expectedAssertionsArray, actualAssertionsArray);
        }
    }
}
