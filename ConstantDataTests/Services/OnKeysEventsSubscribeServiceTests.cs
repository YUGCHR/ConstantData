using ConstantData.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared.Library.Models;

namespace ConstantDataTests.Services
{
    [TestClass()]
    public class OnKeysEventsSubscribeServiceTests
    {
        [TestMethod()]
        [DataRow(300, 500, 500)]
        public void UpdatedValueAssignsToPropertyTest(int source, int value, int expected)
        {
            ConstantsSet constantsSet = new ConstantsSet()
            {
                TaskEmulatorDelayTimeInMilliseconds = new ConstantType()
                {
                    Description = "Description",
                    Value = source,
                    LifeTime = 0.99
                }
            };
            string key = nameof(constantsSet.TaskEmulatorDelayTimeInMilliseconds);

            constantsSet = OnKeysEventsSubscribeService.UpdatedValueAssignsToProperty(constantsSet, key, value);

            var result = constantsSet.TaskEmulatorDelayTimeInMilliseconds.Value;

            Assert.AreEqual(expected, result);
        }
    }
}