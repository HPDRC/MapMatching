/*
 * Name: SqlHelper
 * Author: Guangqiang
 * Version: 2.0
 * Date: 2013-12-31
 */

using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace MapMatchingLib.SysTools
{
    public class SqlHelper
    {
        private string connectionString;
        private SqlConnection myConnection;
        public SqlDataReader Reader;
        private SqlTransaction transaction=null;

        public SqlHelper()
        {
            try
            {
                //connectionString = "Data Source=131.94.133.238;Initial Catalog=ais;Persist Security Info=True;User ID=sa; Password='Akula4less';";
                //connectionString = "Data Source=mountain.cs.fiu.edu;Initial Catalog=ais;Persist Security Info=True;User ID=sa; Password='@kula4less';";
                //connectionString = "Data Source=loader2.cs.fiu.edu;Initial Catalog=ais;Persist Security Info=True;User ID=sa; Password='Akula4less';";
                connectionString = "Data Source=alaska.cs.fiu.edu;Initial Catalog=cityrecorder;Persist Security Info=True;User ID=sa; Password='Akula4less';";
                myConnection = new SqlConnection(connectionString);
            
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing data class." + Environment.NewLine + ex.Message);
            }
        }

        public SqlHelper(string conn)
        {
            try
            {
                connectionString = conn;
                myConnection = new SqlConnection(connectionString);
            
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing data class." + Environment.NewLine + ex.Message);
            }
        }

        public String ConnectionString
        {
            get { return connectionString; }
        }

        public void Dispose()
        {
            try
            {
                if (myConnection != null)
                {
                    if (myConnection.State != ConnectionState.Closed)
                    {
                        myConnection.Close();
                    }
                    myConnection.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error disposing data class." + Environment.NewLine + ex.Message);
            }
        }

        public void Close()
        {
            if (myConnection.State != ConnectionState.Closed) myConnection.Close();
        }
        public object ExecuteScalar(string Command)
        {
            object identity = 0;
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                myCommand.CommandTimeout = 0;
                identity = myCommand.ExecuteScalar();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return identity;
        }

        public object ExecuteScalar(string Command, params SqlParameter[] parameters)
        {
            object identity = 0;
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                for (int x = 0; x < parameters.Length; x++)
                {
                    myCommand.Parameters.Add(parameters[x]);
                }
                myCommand.CommandTimeout = 0;
                identity = myCommand.ExecuteScalar();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return identity;
        }

        public void ExecuteNonQuery(string Command)
        {
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                myCommand.CommandTimeout = 0;
                myCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void ExecuteNonQuery(string Command, params SqlParameter[] parameters)
        {
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                for (int x = 0; x < parameters.Length; x++)
                {
                    myCommand.Parameters.Add(parameters[x]);
                }
                myCommand.CommandTimeout = 0;
                myCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public DataSet GetDataset(string Command)
        {
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                myCommand.CommandTimeout = 0;
                SqlDataAdapter adpt = new SqlDataAdapter(myCommand);
                DataSet ds = new DataSet();
                adpt.Fill(ds);
                return ds;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public DataTable GetDatatable(string Command, string tableName)
        {
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                myCommand.CommandTimeout = 0;
                SqlDataAdapter adpt = new SqlDataAdapter(myCommand);
                DataSet ds = new DataSet();
                adpt.Fill(ds, tableName);
                DataTable dt = new DataTable();
                dt = ds.Tables[tableName];
                return dt;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public SqlDataReader GetReader(string Command)
        {
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                myCommand.CommandTimeout = 0;
                return myCommand.ExecuteReader();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public SqlDataReader GetReader(string Command, params SqlParameter[] parameters)
        {
            try
            {            
                SqlCommand myCommand;
                if (transaction == null)
                {
                    myConnection.Open();
                    myCommand = new SqlCommand(Command, myConnection);
                }
                else
                    myCommand = new SqlCommand(Command, myConnection, transaction);
                for (int x = 0; x < parameters.Length; x++)
                {
                    myCommand.Parameters.Add(parameters[x]);
                }
                myCommand.CommandTimeout = 0;
                return myCommand.ExecuteReader();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Bulk copy save about 90% of the operation time.
        /// With TableLock it can further save 50% more time.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="dt"></param>
        public void BulkCopyWithLock(string table, DataTable dt)
        {
            try
            {
                SqlBulkCopy bulkCopy;
                if (transaction == null)
                {
                    myConnection.Open();
                    bulkCopy = new SqlBulkCopy(connectionString, SqlBulkCopyOptions.TableLock);
                }
                else
                    bulkCopy = new SqlBulkCopy(myConnection, SqlBulkCopyOptions.TableLock, transaction);            
                bulkCopy.BulkCopyTimeout = 0;
                bulkCopy.DestinationTableName = table;
                bulkCopy.WriteToServer(dt);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Bulk copy save about 90% of the operation time.
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="destTable"></param>
        public void BulkCopy(DataTable dt, string destTable)
        {
            try
            {            
                SqlBulkCopy bulkCopy;
                if (transaction == null)
                {
                    myConnection.Open();
                    bulkCopy = new SqlBulkCopy(connectionString, SqlBulkCopyOptions.TableLock);
                }
                else
                    bulkCopy = new SqlBulkCopy(myConnection, SqlBulkCopyOptions.TableLock, transaction);
                bulkCopy.BulkCopyTimeout = 0;
                bulkCopy.DestinationTableName = destTable;
                bulkCopy.WriteToServer(dt);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public string ReduceLineString(string lineStr,double tolerance)
        {
            string reducedStr = "";
            myConnection.Open();
            SqlCommand myCommand = new SqlCommand("SELECT GEOMETRY::STLineFromText('" + lineStr + "',0).MakeValid().Reduce(" + tolerance.ToString() + ").ToString()", myConnection);
            myCommand.CommandTimeout = 0;
            reducedStr= (string)myCommand.ExecuteScalar();
            myConnection.Close();
            Regex rgx = new Regex(@"POINT \([\+-]?\d+\.\d+ [\+-]?\d+\.\d+\)(, )?");
            reducedStr= rgx.Replace(reducedStr, "").Replace("LINESTRING ", "");
            reducedStr = "LINESTRING(" + reducedStr.Substring(reducedStr.IndexOf('(')).Replace("(", "").Replace(")", "").Replace(", ", ",").TrimEnd(',') + ")";
            return reducedStr;
        }

        public void BeginTransaction()
        {
            myConnection.Open();
            transaction=myConnection.BeginTransaction();
        }

        public void CommitTransaction()
        {
            transaction.Commit();
            transaction = null;
        }
    }
}
