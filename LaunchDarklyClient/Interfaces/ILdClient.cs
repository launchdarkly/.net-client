﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient.Interfaces
{
	public interface ILdClient
	{
		void Dispose();
		void Flush();
		void Identify(User user);
		bool Initialized();
		int IntVariation(string key, User user, int defaultValue);
		float FloatVariation(string key, User user, float defaultValue);
		JToken JsonVariation(string key, User user, JToken defaultValue);
		string StringVariation(string key, User user, string defaultValue);
		bool BoolVariation(string key, User user, bool defaultValue = false);
		void Track(string name, User user, string data);
		IDictionary<string, JToken> AllFlags(User user);
		string SecureModeHash(User user);

		[Obsolete("Please use BoolVariation instead.")]
		bool Toggle(string key, User user, bool defaultValue = false);
	}
}