﻿using System.Data;
using System.Data.Common;

namespace WillSoss.Data
{
    public static class DbConnectionExtensions
	{
		public static async Task EnsureOpenAsync(this DbConnection db)
		{
			if (db.State != ConnectionState.Open)
				await db.OpenAsync();
		}
	}
}