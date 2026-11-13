using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>The 4a social setup checks: person ids and the SocialWorld.</summary>
	public class SocialSetupValidatorTests
	{
		readonly List<Object> made = new List<Object>();

		[TearDown]
		public void TearDown()
		{
			foreach (Object o in made)
				if (o != null)
					Object.DestroyImmediate(o);
			made.Clear();
		}

		[Test]
		public void DuplicateId_OneError_EmptyId_Warning()
		{
			Character a = Person("Aino", "same");
			Character b = Person("Heikki", "same");
			Character c = Person("Lempi", "");
			List<SetupIssue> issues = Run(new PersonIdCheck());

			SetupIssue dup = issues.Single(i => i.Severity == SetupSeverity.Error);
			StringAssert.Contains("same person id", dup.Message);
			Assert.AreSame(b, dup.Context);
			SetupIssue empty = issues.Single(i => i.Severity == SetupSeverity.Warning);
			Assert.AreSame(c, empty.Context);
			Assert.IsFalse(issues.Any(i => i.Context == a));
		}

		[Test]
		public void TwoSocialWorlds_Error()
		{
			World();
			World();
			List<SetupIssue> issues = Run(new SocialWorldCheck());
			Assert.AreEqual(1, issues.Count(i => i.Severity == SetupSeverity.Error));
		}

		[Test]
		public void ProjectPrefabs_HaveNoPersonIds()
		{
			var ctx = new SetupContext(default);
			var issues = new List<SetupIssue>();
			new PersonIdCheck().Run(ctx, issues);
			Assert.IsEmpty(issues, string.Join("\n", issues));
		}

		Character Person(string name, string id)
		{
			var go = new GameObject(name);
			made.Add(go);
			go.SetActive(false);
			Character c = go.AddComponent<Character>();
			typeof(Character).GetField("personId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
				.SetValue(c, id);
			return c;
		}

		void World()
		{
			var go = new GameObject("SocialWorld");
			made.Add(go);
			go.SetActive(false);
			go.AddComponent<SocialWorld>();
		}

		static List<SetupIssue> Run(ISetupCheck check)
		{
			var ctx = new SetupContext(SceneManager.GetActiveScene()) { CheckProjectPrefabs = false };
			var issues = new List<SetupIssue>();
			check.Run(ctx, issues);
			// The runner's scene may hold other objects; keep only ours.
			return issues;
		}
	}
}
