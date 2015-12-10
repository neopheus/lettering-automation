﻿using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;

namespace Lettering {
    internal class DataReader {
        public static DataTable getCsvData() {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            openFileDialog.Filter = "csv file (*.csv)|*.csv|txt file (*.txt)|*.txt";
            openFileDialog.RestoreDirectory = true;

            //try {
                if(openFileDialog.ShowDialog() == DialogResult.OK) {
                    //string pathOnly = Path.GetDirectoryName(openFileDialog.FileName);
                    string fileName = Path.GetFileName(openFileDialog.FileName);

                    System.IO.Directory.CreateDirectory(Program.tempFolder);
                    System.IO.File.Copy(openFileDialog.FileName, Program.tempFolder + fileName, true);

                    string query = @"SELECT * FROM [" + fileName + "]";

                    using(OdbcConnection conn = new OdbcConnection("Driver={Microsoft Text Driver (*.txt; *.csv)};DBQ=" + Program.tempFolder)) {
                        OdbcCommand command = new OdbcCommand(query, conn);
                        OdbcDataAdapter adapter = new OdbcDataAdapter(command);

                        DataTable dataTable = new DataTable();
                        
                        DataColumn[] cols = {
                                                new DataColumn("HOUSE", typeof(String)), 
                                                new DataColumn("ORDER_NO", typeof(int)), 
                                                new DataColumn("ORDER_VOUCH", typeof(int)), 
                                                new DataColumn("PARENT_VOUCH", typeof(int)), 
                                                new DataColumn("ITEM_NO", typeof(String)), 
                                                new DataColumn("LETTER_SIZE", typeof(double)), 
                                                new DataColumn("LETTER_SPEC", typeof(double)), 
                                                new DataColumn("DRAWING_LETTER_WORD1", typeof(String)), 
                                                new DataColumn("DRAWING_LETTER_WORD2", typeof(String)), 
                                                new DataColumn("DRAWING_LETTER_WORD3", typeof(String)), 
                                                new DataColumn("DRAWING_LETTER_WORD4", typeof(String)), 
                                                new DataColumn("SCHEDULE_DATE_CCYYMMDD", typeof(int)),
                                                new DataColumn("SCHEDULE_DATE_MMDDCCYY", typeof(DateTime)),
                                                new DataColumn("NAME", typeof(String))
                                            };
                        dataTable.Columns.AddRange(cols);
                        
                        try {
                            adapter.Fill(dataTable);
                        } catch(InvalidCastException) {

                            DataTable dtClone = dataTable.Clone();
                            dtClone.Columns["HOUSE"].DataType = typeof(String);
                            dtClone.Columns["SCHEDULE_DATE_MMDDCCYY"].DataType = typeof(DateTime);
                            dtClone.Columns["ORDER_NO"].DataType = typeof(Int32);
                            dtClone.Columns["ORDER_VOUCH"].DataType = typeof(Int32);
                            dtClone.Columns["ITEM_NO"].DataType = typeof(String);
                            dtClone.Columns["LETTER_SIZE"].DataType = typeof(Double);
                            dtClone.Columns["LETTER_SPEC"].DataType = typeof(Double);
                            dtClone.Columns["DRAWING_LETTER_WORD1"].DataType = typeof(String);
                            dtClone.Columns["DRAWING_LETTER_WORD2"].DataType = typeof(String);
                            dtClone.Columns["DRAWING_LETTER_WORD3"].DataType = typeof(String);
                            dtClone.Columns["DRAWING_LETTER_WORD4"].DataType = typeof(String);
                            dtClone.Columns["NAME"].DataType = typeof(String);

                            foreach(DataRow row in dataTable.Rows) {
                                object[] vals = row.ItemArray;

                                vals[0] = ((String)vals[0]).Replace("\"", "");                      // HOUSE
                                vals[1] = int.Parse(((String)vals[1]).Replace("\"", ""));           // ORDER_NO
                                vals[2] = int.Parse(((String)vals[2]).Replace("\"", ""));           // ORDER_VOUCH
                                vals[3] = int.Parse(((String)vals[3]).Replace("\"", ""));           // PARENT_VOUCH
                                vals[4] = ((String)vals[4]).Replace("\"", "");                      // ITEM_NO
                                vals[5] = double.Parse(((String)vals[5]).Replace("\"", ""));        // LETTER_SIZE
                                vals[6] = double.Parse(((String)vals[6]).Replace("\"", ""));        // LETTER_SPEC
                                vals[7] = ((String)vals[7]).Replace("\"", "");                      // DRAWING_LETTER_WORD1
                                vals[8] = ((String)vals[8]).Replace("\"", "");                      // DRAWING_LETTER_WORD2
                                vals[9] = ((String)vals[9]).Replace("\"", "");                      // DRAWING_LETTER_WORD3
                                vals[10] = ((String)vals[10]).Replace("\"", "");                    // DRAWING_LETTER_WORD4
                                vals[11] = int.Parse(((String)vals[11]).Replace("\"", ""));         // SCHEDULE_DATE_CCYYMMDD
                                vals[12] = DateTime.Parse(((String)vals[12]).Replace("\"", ""));    // SCHEDULE_DATE_MMDDCCYY
                                vals[13] = ((String)vals[13]).Replace("\"", "");                    // NAME

                                dtClone.NewRow().ItemArray = vals;
                            }

                            unifyHeaders(dtClone);
                            return dtClone;
                        }

                        System.IO.Directory.Delete(Program.tempFolder, true);

                        unifyHeaders(dataTable);
                        return dataTable;
                    }
                } else {
                    //MessageBox.Show("No file chosen.");

                    return null;
                }
            /*
            } catch(Exception ex) {     // should use specific exceptions
                MessageBox.Show("Error: Could not read file.\n\n" + ex.Message);

                return null;
            }
             * */
        }

        public static DataTable runReport() {
            string connectionString = "Driver={iSeries Access ODBC Driver}; System=USC; SignOn=4;";

            List<DateTime> holidays = ReadHolidays();

            try {
                using(OdbcConnection conn = new OdbcConnection(connectionString)) {
                    conn.Open();

                    DateTime date = DateTime.Today.AddDays(-1);

                    string query = @"
                        SELECT det.dhous, det.scdat, det.endat, det.ordnr, det.orvch, 
                               det.ditem, det.dlsiz, siz.letwid, nam.letname, 
                               det.dlwr1, det.dlwr2, det.dlwr3, det.dlwr4, 
                               CASE WHEN det.ditem LIKE 'SIGN%' THEN clr.gclr ELSE TRIM(det.dclr1) END AS dclr1, det.dclr2, det.dclr3, det.dclr4, det.rudat
                        FROM (
                              SELECT d.dhous,
                                     CASE WHEN d.dscmo = 0 THEN NULL ELSE DATE(d.dsccy||d.dscyr||'-'||RIGHT('00'||d.dscmo, 2)||'-'||RIGHT('00'||d.dscda, 2)) END AS scdat, 
                                     DATE(d.dorcy||d.doryr||'-'||RIGHT('00'||d.dormo, 2)||'-'||RIGHT('00'||d.dorda, 2)) AS endat, 
                                     d.ordnr, d.orvch, d.dpvch, d.ditem, d.dlsiz, 
                                     d.dlwr1, d.dlwr2, d.dlwr3, d.dlwr4, 
                                     d.dclr1, d.dclr2, d.dclr3, d.dclr4, 
                                     CASE d.drumo WHEN 0 THEN NULL ELSE DATE(d.drucy||d.druyr||'-'||RIGHT('00'||d.drumo, 2)||'-'||RIGHT('00'||d.druda, 2)) END AS rudat 

                              FROM VARSITYF.DETAIL AS d
                              WHERE (" +
                                        String.Format("((d.dorcy = {0}) AND (d.doryr = {1}) AND (d.dormo = {2}) AND (d.dorda = {3}))", 
                                                     date.Year / 100, date.Year % 100, date.Month, date.Day);

                                        //NOTE(adam): add days to search for to cover no working days
                                        while(date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday || holidays.Contains(date)) {
                                            date = date.AddDays(-1);
                                            query += String.Format(" OR ((d.dorcy = {0}) AND (d.doryr = {1}) AND (d.dormo = {2}) AND (d.dorda = {3}))", 
                                                                   date.Year / 100, date.Year % 100, date.Month, date.Day); 
                                        }

                                        query += @") AND 
                                    ((d.dclas IN ('041', '049', '04C', '04D', '04Y', 'F09', 'PS3', 'L02', 'L05', 'L10', 'S03', 'SKL', 'VTT', '04G')) OR
                                     (d.ditem LIKE 'SIGN%')) AND 
                                    (d.ditem NOT LIKE 'OZ%') AND (d.ditem NOT LIKE 'COZ%') AND 
                                    (d.ditem NOT LIKE 'SP%') AND 
                                    (d.ditem NOT LIKE 'IDC%') AND
                                    (d.dscda > 0)
                        ) AS det

                        LEFT JOIN 
                                    DJLIBR.ORD_NAM_C 
                         AS nam
                        ON det.ordnr = nam.ordnr AND det.orvch = nam.orvch AND nam.letname <> ''

                        LEFT JOIN (
                                    SELECT DISTINCT s.ordnr, s.orvch, s.letwid
                                    FROM VARSITYF.HLDSIZ AS s
                        ) AS siz
                        ON det.ordnr = siz.ordnr AND det.dpvch = siz.orvch

                        LEFT JOIN 
                                    VARSITYF.HLDCLR
                         AS clr
                        ON det.ordnr = clr.ordnr AND det.orvch = clr.orvch AND clr.itseq = 2

                        ORDER BY det.ditem";

                    OdbcCommand command = new OdbcCommand(query);
                    command.Connection = conn;
                    OdbcDataAdapter adapter = new OdbcDataAdapter(command);

                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    conn.Close();
                    return table;
                }
            } catch(OdbcException e) {
                if(e.Errors[0].SQLState == "IM002") {
                    MessageBox.Show("Driver not found.\n\nPlease contact the IT Department to install the ODBC Driver for IBM iSeries Access.", "Driver Not Found");
                    return null;
                } else {
                    string errorLog = "";
                    for(int i = 0; i != e.Errors.Count; ++i) {
                        errorLog += ("Error " + (i + 1) + " of " + e.Errors.Count + "\n");
                        errorLog += ("SQLState:  " + e.Errors[i].SQLState + "\n");
                        errorLog += ("NativErr:  " + e.Errors[i].NativeError + "\n");
                        errorLog += ("EMessage:  " + e.Errors[i].Message + "\n");
                        errorLog += ("ESource:   " + e.Errors[i].Source + "\n\n");
                    }
                    MessageBox.Show(errorLog, "Errors Encountered");
                    return null;
                }
            } catch(Exception e) {
                MessageBox.Show("Exception: " + e.Message);
                return null;
            }
        }

        private static void unifyHeaders(DataTable data) {
            if(data.Columns.Contains("HOUSE") && !data.Columns.Contains(QueryHeaders.CUT_HOUSE)) data.Columns["HOUSE"].ColumnName = QueryHeaders.CUT_HOUSE;
            if(data.Columns.Contains("SCHEDULE_DATE_MMDDCCYY") && !data.Columns.Contains(QueryHeaders.SCHEDULE_DATE)) data.Columns["SCHEDULE_DATE_MMDDCCYY"].ColumnName = QueryHeaders.SCHEDULE_DATE;
            if(!data.Columns.Contains(QueryHeaders.ENTER_DATE)) data.Columns.Add(QueryHeaders.ENTER_DATE);
            if(data.Columns.Contains("ORDER_NO") && !data.Columns.Contains(QueryHeaders.ORDER_NUMBER)) data.Columns["ORDER_NO"].ColumnName = QueryHeaders.ORDER_NUMBER;
            if(data.Columns.Contains("ORDER_VOUCH") && !data.Columns.Contains(QueryHeaders.VOUCHER)) data.Columns["ORDER_VOUCH"].ColumnName = QueryHeaders.VOUCHER;
            if(data.Columns.Contains("ITEM_NO") && !data.Columns.Contains(QueryHeaders.ITEM)) data.Columns["ITEM_NO"].ColumnName = QueryHeaders.ITEM;
            if(data.Columns.Contains("LETTER_SIZE") && !data.Columns.Contains(QueryHeaders.SIZE)) data.Columns["LETTER_SIZE"].ColumnName = QueryHeaders.SIZE;
            if(data.Columns.Contains("LETTER_SPEC") && !data.Columns.Contains(QueryHeaders.SPEC)) data.Columns["LETTER_SPEC"].ColumnName = QueryHeaders.SPEC;
            if(data.Columns.Contains("NAME") && !data.Columns.Contains(QueryHeaders.NAME)) data.Columns["NAME"].ColumnName = QueryHeaders.NAME;
            if(data.Columns.Contains("DRAWING_LETTER_WORD1") && !data.Columns.Contains(QueryHeaders.WORD1)) data.Columns["DRAWING_LETTER_WORD1"].ColumnName = QueryHeaders.WORD1;
            if(data.Columns.Contains("DRAWING_LETTER_WORD2") && !data.Columns.Contains(QueryHeaders.WORD2)) data.Columns["DRAWING_LETTER_WORD2"].ColumnName = QueryHeaders.WORD2;
            if(data.Columns.Contains("DRAWING_LETTER_WORD3") && !data.Columns.Contains(QueryHeaders.WORD3)) data.Columns["DRAWING_LETTER_WORD3"].ColumnName = QueryHeaders.WORD3;
            if(data.Columns.Contains("DRAWING_LETTER_WORD4") && !data.Columns.Contains(QueryHeaders.WORD4)) data.Columns["DRAWING_LETTER_WORD4"].ColumnName = QueryHeaders.WORD4;
            if(!data.Columns.Contains(QueryHeaders.COLOR1)) data.Columns.Add(QueryHeaders.COLOR1);
            if(!data.Columns.Contains(QueryHeaders.COLOR2)) data.Columns.Add(QueryHeaders.COLOR2);
            if(!data.Columns.Contains(QueryHeaders.COLOR3)) data.Columns.Add(QueryHeaders.COLOR3);
            if(!data.Columns.Contains(QueryHeaders.COLOR4)) data.Columns.Add(QueryHeaders.COLOR4);
            if(!data.Columns.Contains(QueryHeaders.RUSH_DATE)) data.Columns.Add(QueryHeaders.RUSH_DATE);

            //data.Columns["PARENT_VOUCH"].ColumnName = "";
            //data.Columns["SCHEDULE_DATE_CCYYMMDD"].ColumnName = "";
        }

        private static List<DateTime> ReadHolidays() {
            List<DateTime> holidays = new List<DateTime>();

            using(StreamReader sr = new StreamReader(@"./configs/holidays.txt")) {
                string line;
                while(sr.Peek() > -1) {
                    line = sr.ReadLine().Trim();

                    try {
                        holidays.Add(DateTime.Parse(line));
                    } catch(FormatException ex) {
                        continue;
                    }
                }
            }

            return holidays;
        }
    }
}