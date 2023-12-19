namespace SudokuCS
{
    public class Sudoku
    {
        private static readonly List<int> VALS = new(){1,2,3,4,5,6,7,8,9};
        private List<Cell> _cells = new();
        private List<Cells_Group> _rows = new();
        private List<Cells_Group> _columns = new();
        private List<Cells_Group> _blocks = new();
        private List<List<int>> _starting_schema = new();

        public int _stats_check_for_twins_usage = 0;
        public int _stats_check_for_hidden_single = 0;
        public int _stats_check_for_number_on_same_line = 0;
        public int _stats_try_random_cell = 0;
        public long _stats_fill_time_cost_us = 0;

        private class Cell
        {
            public int _val;
            public List<int> _available_vals = new();

            public Cells_Group _row;
            public Cells_Group _column;
            public Cells_Group _block;

            public Cell(Cells_Group row,Cells_Group column,Cells_Group block, int val=0)
            {
                _row = row;
                _column = column;
                _block = block;
                _val = val;
                Init_Available_Vals();
            }

            public void Init_Available_Vals()
            {
                if (_val == 0)
                {
                    _available_vals = new(VALS  .Except(from cell in _row._cells select cell._val)
                                                .Except(from cell in _column._cells select cell._val)
                                                .Except(from cell in _block._cells select cell._val));
                }
                else _available_vals.Clear();
            }

            public (int,int) Position => (_row._idx,_column._idx);

            public string Available_Vals_Str()
            {
                string return_val = "\n(";
                foreach (var val in _available_vals)
                {
                    return_val = return_val + val.ToString() + ",";
                }
                return_val = return_val.Remove(return_val.Count()-1) + ")";
                return return_val;
            }

            public bool Is_Correct() => _val != 0 || _available_vals.Count>0;

            public static int GetCellIdx(Cell cell) => Cell.GetPositionIdx(cell.Position);
            public static int GetPositionIdx((int,int) position) => ((position.Item1-1)*VALS.Count + position.Item2);
        }

        private class Cells_Group
        {
            public readonly int _idx;
            public List<Cell> _cells = new();
            
            public Cells_Group(int idx)
            {
                _idx = idx;
            }

            public Dictionary<int,List<(int,int)>> Calc_Vals_Positions()
            {
                // This dictionary has 1,2,3,4,5... as keys (which are all possible values in sudoku VALS) and a list of positions as value
                Dictionary<int,List<(int,int)>> return_val = new();
                foreach (var val in VALS) // foreach possible number in the sudoku
                {
                    return_val.Add(val,(from cell in _cells where cell._available_vals.Contains(val) select cell.Position).OrderBy(Pos => (Pos.Item1,Pos.Item2)).ToList());
                }
                // Here the dictionary should be something like:
                /* 
                {    
                    1:{(1,2),(1,3)}
                    3:{(2,3),(4,5),(7,8)}
                    4:{(1,2),(1,1),(4,5),(4,6)}
                }
                where key are the value and the list contains all possible position of that value
                */
                return return_val;
            }

            public bool Is_Correct() => _cells.Where(cell => cell._val != 0).Count() == _cells.Where(cell => cell._val != 0).Distinct().Count();
            
        }
    
        public Sudoku(List<List<int>> starting_schema)
        {
            _starting_schema = starting_schema;

            // Create 9 rows, 9 columns and 9 blocks
            for (int i = 1; i <= 9; i++)
            {
                _rows.Add(new(i));
                _columns.Add(new(i));
                _blocks.Add(new(i));
            }

            // Create cells
            int block_idx = 0;
            for (int r = 1; r <= 9; r++)
            {
                for (int c = 1; c <= 9; c++)
                {
                    if (r<=3 && c<=3) block_idx = 1;
                    if (r<=3 && c>=4 && c<=6) block_idx = 2;
                    if (r<=3 && c>=7) block_idx = 3;
                    if (r>=4 && r<=6 && c<=3) block_idx = 4;
                    if (r>=4 && r<=6 && c>=4 && c<=6) block_idx = 5;
                    if (r>=4 && r<=6 && c>=7) block_idx = 6;
                    if (r>=7 && c<=3) block_idx = 7;
                    if (r>=7 && c>=4 && c>=4 && c<=6) block_idx = 8;
                    if (r>=7 && c>=7) block_idx = 9;

                    _cells.Add(new(_rows[r-1],_columns[c-1],_blocks[block_idx-1]));
                }
            }

            // Link cells in rows
            foreach (var row in _rows)
            {
                row._cells = (from cell in _cells where cell._row._idx == row._idx orderby cell._column._idx ascending select cell).ToList();
            }
            // Link cells in columns
            foreach (var column in _columns)
            {
                column._cells = (from cell in _cells where cell._column._idx == column._idx orderby cell._row._idx ascending select cell).ToList();
            }
            // Link cells in blocks
            foreach (var block in _blocks)
            {
                block._cells = (from cell in _cells where cell._block._idx == block._idx orderby cell._column._idx ascending, cell._row._idx ascending select cell).ToList();
            }

            ResetSchema();
        }

        private bool Set_Single()
        {
            bool new_val_flag = false;
            
            foreach (var cell in _cells)
            {
                if (cell._val == 0)
                {
                    if (cell._available_vals.Count == 1) 
                    {
                        new_val_flag = true;
                        cell._val = cell._available_vals[0];
                        // Update cells in the same group of this
                        foreach (var c in cell._block._cells)
                        {
                            if (c.Position != cell.Position) c._available_vals.RemoveAll(val => val == cell._val);
                        }
                        foreach (var c in cell._row._cells)
                        {
                            if (c.Position != cell.Position) c._available_vals.RemoveAll(val => val == cell._val);
                        }
                        foreach (var c in cell._column._cells)
                        {
                            if (c.Position != cell.Position) c._available_vals.RemoveAll(val => val == cell._val);
                        }
                    }
                }
            }
            return new_val_flag;
        }
        
        private bool Check_For_Twins(ref List<Cells_Group> groups)
        {
            bool change_flag = false;
            Dictionary<int,List<(int,int)>> vals_pos = new();
            foreach (var grp in groups) // foreach group in the list
            {
                // Check for 2 values which are available in only 2 cells then remove other available values in that cells
                vals_pos = grp.Calc_Vals_Positions();
                foreach (var item1 in vals_pos.Where(item => item.Value.Count==2))
                {
                    foreach (var item2 in vals_pos.Where(item => Enumerable.SequenceEqual(item1.Value,item.Value) && !item1.Equals(item)))
                    {
                        //Found!!! Remove other values from available values
                        foreach (var cell in grp._cells.Where(cell => item1.Value.Contains(cell.Position)))
                        {
                            if (cell._available_vals.RemoveAll(val => val != item1.Key && val != item2.Key) >0)
                            {
                                change_flag = true;
                                _stats_check_for_twins_usage += 1;
                            }
                        }
                    }
                }

                // Check for cells which has identical available vals with 2 value, then remove that 2 values from other group cells
                foreach (var twin_cell1 in grp._cells.Where(cell => cell._available_vals.Count == 2))
                {
                    foreach (var twin_cell2 in grp._cells.Where(cell => Enumerable.SequenceEqual(twin_cell1._available_vals,cell._available_vals) && !cell.Equals(twin_cell1)))
                    {
                        foreach (var other_cell in grp._cells.Where(cell => !cell.Equals(twin_cell1) && !cell.Equals(twin_cell2)))
                        {
                            if (other_cell._available_vals.RemoveAll(val => twin_cell1._available_vals.Contains(val))>0)
                            {
                                change_flag = true;
                                _stats_check_for_twins_usage += 1;
                            }
                        }
                    }
                }
            }
            return change_flag;
        }

        private bool Check_For_Hidden_Single(ref List<Cells_Group> groups)
        {
            bool change_flag = false;
            
            foreach (var grp in groups)
            {
                foreach (var val in VALS) // foreach possible number in the sudoku
                {
                    if (grp._cells.Count(cell => cell._available_vals.Contains(val) && cell._available_vals.Count>1) == 1)
                    {
                        grp._cells.First(cell => cell._available_vals.Contains(val) && cell._available_vals.Count>1)._available_vals.RemoveAll(value => value != val);
                        change_flag = true;
                        _stats_check_for_hidden_single += 1;
                    }
                }
            }
            return change_flag;
        }

        private bool Check_For_Number_On_Same_Line(ref List<Cells_Group> groups)
        {
            bool change_flag = false;
            bool is_block = false;
            int old_stats_check_for_number_on_same_line = _stats_check_for_number_on_same_line;
            
            foreach (var grp in groups) // foreach group in the list
            {      
                // only blocks are ok for this alghorithm   
                if (!is_block) is_block = grp._cells.Any(cell => cell._row._idx != grp._cells[0]._row._idx && cell._column._idx != grp._cells[0]._column._idx);
                if (!is_block) return false;   
                
                // Now I have to search for numbers which positions are on the same line (so with the same row or the same column)
                foreach (var item in grp.Calc_Vals_Positions())
                {
                    if (item.Value.Count >=2)
                    {
                        if (item.Value.All(Pos => Pos.Item1 == item.Value[0].Item1)) //Check if all row of positions are equal
                        {
                            foreach(var cell in grp._cells.First(cell => cell.Position.Item1 == item.Value[0].Item1)._row._cells) // Foreach cell in selected the row 
                            {
                                if (!grp.Equals(cell._block)) // If this cell is not in this block
                                {   
                                    if (cell._available_vals.Contains(item.Key))
                                    {                          
                                        cell._available_vals.RemoveAll(value => value == item.Key);
                                        if (_stats_check_for_number_on_same_line == old_stats_check_for_number_on_same_line) _stats_check_for_number_on_same_line += 1;
                                        
                                        change_flag = true;
                                    }
                                }
                            }
                        }

                        if (item.Value.All(Pos => Pos.Item2 == item.Value[0].Item2)) //Check if all column of positions are equal
                        {
                            foreach(var cell in grp._cells.First(cell => cell.Position.Item2 == item.Value[0].Item2)._column._cells) // Foreach cell in selected the column 
                            {
                                if (!grp.Equals(cell._block)) // If this cell is not in this block
                                {
                                    if (cell._available_vals.Contains(item.Key))
                                    {
                                        cell._available_vals.RemoveAll(value => value == item.Key);
                                        if (_stats_check_for_number_on_same_line == old_stats_check_for_number_on_same_line) _stats_check_for_number_on_same_line += 1;
                                        change_flag = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return change_flag;
        }

        private (Sudoku,int) Recursive_Set_Random_Cell(Sudoku sudoku,Cell cell)
        {
            cell._val = cell._available_vals[0];
            cell._available_vals.RemoveAt(0);
            sudoku.Logic_Fill();
            if (!sudoku.Is_Correct())
            {
                if (cell._available_vals.Count == 0) return (sudoku,0);  // There is an error with this branch!!
                else
                {
                    return sudoku.Recursive_Set_Random_Cell(sudoku,cell); // Try another value in this cell
                }
            }
            else
            {
                if(sudoku.Is_Complete()) return (sudoku,1); // Correct branch!
                else 
                {
                    return sudoku.Recursive_Set_Random_Cell(sudoku,sudoku._cells.First(other_cell => Cell.GetCellIdx(other_cell) > Cell.GetCellIdx(cell) && other_cell._val == 0 && other_cell._available_vals.Count>0)); // Try with another cell
                }
            }
        }

        private bool Try_Random_Cell(Sudoku sudoku)
        {
            bool change_flag = false;

            foreach (var cell in sudoku._cells.Where(cell => cell._available_vals.Count>=2).OrderBy(cell => cell._available_vals.Count))
            {
                foreach (var val in cell._available_vals)
                {
                    // Create a new sudoku as test copied from the original one
                    Sudoku test_sudoku = new(sudoku.ExportSchema());
                    foreach (var test_cell in test_sudoku._cells)
                    {
                        test_cell._available_vals.Clear();
                        foreach (var value in sudoku._cells.First(original_cell => original_cell.Position == test_cell.Position)._available_vals)
                        {
                            test_cell._available_vals.Add(value);
                        }  
                    }

                    // tring to remove the first element, then check for error
                    int return_val = 0;
                    (test_sudoku, return_val) = Recursive_Set_Random_Cell(test_sudoku,cell);
 
                }
            }

            return change_flag;
        }

        private void ResetSchema()
        {
            foreach (var row in _rows)
            {
                for (int i = 1; i <= 9; i++)
                {
                    row._cells[i-1]._val = _starting_schema[row._idx-1][i-1];
                }
            }
            // Reset all available values in cells
            foreach (var cell in _cells)
            {
                cell.Init_Available_Vals();
            }
        }

        public void BruteForce_Fill()
        {
            // Create a new sudoku as test copied from the original one
            Sudoku test_sudoku = new(ExportSchema());
            foreach (var test_cell in test_sudoku._cells)
            {
                test_cell._available_vals.Clear();
                foreach (var value in _cells.First(original_cell => original_cell.Position == test_cell.Position)._available_vals)
                {
                    test_cell._available_vals.Add(value);
                }  
            }

            int result = 0;
            (test_sudoku,result) = Recursive_Set_Random_Cell(this,this._cells.First(cell => cell._val==0 && cell._available_vals.Count>0));

            foreach (var cell in test_sudoku._cells)
            {
                _cells.First(this_cell => this_cell.Position == cell.Position)._val = cell._val;
            }
        }

        public void Fill()
        {
            Logic_Fill();
            if (!Is_Complete()) BruteForce_Fill();
        }

        public void Logic_Fill()
        {
            long start_tick = DateTime.Now.Ticks;
            long cycle_edit_cnt = 0;
            do
            {   
                cycle_edit_cnt = 0;
                while (Set_Single()){cycle_edit_cnt += 1;};
                
                while (Check_For_Hidden_Single(ref _rows)){cycle_edit_cnt += 1;while (Set_Single()){};}
                while (Check_For_Hidden_Single(ref _columns)){cycle_edit_cnt += 1;while (Set_Single()){};}
                while (Check_For_Hidden_Single(ref _blocks)){cycle_edit_cnt += 1;while (Set_Single()){};}

                while (Check_For_Twins(ref _rows)){cycle_edit_cnt += 1;while (Set_Single()){};}
                while (Check_For_Twins(ref _columns)){cycle_edit_cnt += 1;while (Set_Single()){};}
                while (Check_For_Twins(ref _blocks)){cycle_edit_cnt += 1;while (Set_Single()){};}

                while (Check_For_Number_On_Same_Line(ref _blocks)){cycle_edit_cnt += 1;while (Set_Single()){};}

            }while(!Is_Complete() && cycle_edit_cnt > 0);

            _stats_fill_time_cost_us = (DateTime.Now.Ticks - start_tick)/10;
        }

        public bool Is_Correct()
        {
            // return  _rows.All(row => Enumerable.SequenceEqual(row._cells.OrderBy(cell => cell._val).Select(cell => cell._val),VALS.OrderBy(val => val))  )
            //         && _columns.All(column => Enumerable.SequenceEqual(column._cells.OrderBy(cell => cell._val).Select(cell => cell._val),VALS.OrderBy(val => val))  )
            //         && _blocks.All(block => Enumerable.SequenceEqual(block._cells.OrderBy(cell => cell._val).Select(cell => cell._val),VALS.OrderBy(val => val))  );
        
            return  _rows.All(row => row.Is_Correct())  // Check if row doen't have duplicates
                    && _columns.All(column => column.Is_Correct() )    // Check if column doen't have duplicates
                    && _blocks.All(block => block.Is_Correct())  // Check if block doen't have duplicates
                    && _cells.All(cell => cell.Is_Correct());  // Check if all cells has a non-zero value or available values 
        }
        
        public bool Is_Complete()
        {
            return  _cells.All(cell => cell._val != 0 );
        }
        

        public override string ToString()
        {
            int cnt=0;
            string return_val="\n";
            foreach (var cell in _cells)
            {
                cnt = cnt + 1;
                return_val = return_val + " " + cell._val.ToString()+" ";
                if (cnt%3 == 0) return_val = return_val + "|";
                if (cnt%27 == 0) return_val = return_val + "\n-----------------------------";
                if (cnt%9 == 0) return_val = return_val + "\n";
            }
            return_val = return_val + $"Is_Correct: {this.Is_Correct()}";
            return_val = return_val + $"\nIs_Complete: {this.Is_Complete()}";
            return_val = return_val + $"\nCheck_For_Hidden_Single usage: {_stats_check_for_hidden_single}";
            return_val = return_val + $"\nCheck_For_Twins usage: {_stats_check_for_twins_usage}";
            return_val = return_val + $"\nCheck_For_Number_On_Same_Line usage: {_stats_check_for_number_on_same_line}";
            return_val = return_val + $"\nTry_Random_Cell usage: {_stats_try_random_cell}";
            return_val = return_val + $"\nTime cost: {_stats_fill_time_cost_us}us";
            return_val = return_val + "\n";
            return return_val;
        }

        public List<List<int>> ExportSchema()
        {
            List<List<int>> schema = new();
            foreach (var row in _rows)
            {
                schema.Add(new(from cell in row._cells orderby cell._column._idx select cell._val));
            }
            return schema;
        }
    
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            List<List<int>> starting_schema = new();
            // HARD
            //starting_schema.Add(new(){0,0,4, 8,6,0, 0,3,0});
            //starting_schema.Add(new(){0,0,1, 0,0,0, 0,9,0});
            //starting_schema.Add(new(){8,0,0, 0,0,9, 0,6,0});
            //starting_schema.Add(new(){5,0,0, 2,0,6, 0,0,1});
            //starting_schema.Add(new(){0,2,7, 0,0,1, 0,0,0});
            //starting_schema.Add(new(){0,0,0, 0,4,3, 0,0,6});
            //starting_schema.Add(new(){0,5,0, 0,0,0, 0,0,0});
            //starting_schema.Add(new(){0,0,9, 0,0,0, 4,0,0});
            //starting_schema.Add(new(){0,0,0, 4,0,0, 0,1,5});
            // VERY HARD
            starting_schema.Add(new(){0,0,5, 0,0,0, 0,6,2});
            starting_schema.Add(new(){0,6,3, 0,0,9, 0,0,0});
            starting_schema.Add(new(){0,0,0, 0,0,0, 0,0,4});
            starting_schema.Add(new(){0,0,0, 0,0,6, 7,0,3});
            starting_schema.Add(new(){0,0,6, 7,0,5, 0,0,0});
            starting_schema.Add(new(){1,0,0, 8,0,0, 0,0,0});
            starting_schema.Add(new(){8,0,1, 2,0,0, 6,0,0});
            starting_schema.Add(new(){0,0,0, 0,0,0, 5,3,0});
            starting_schema.Add(new(){0,4,0, 0,0,0, 8,0,0});
            
            // THE HARDEST
            // starting_schema.Add(new(){0,0,5, 3,0,0, 0,0,0});
            // starting_schema.Add(new(){8,0,0, 0,0,0, 0,2,0});
            // starting_schema.Add(new(){0,7,0, 0,1,0, 5,0,0});
            // starting_schema.Add(new(){4,0,0, 0,0,5, 3,0,0});
            // starting_schema.Add(new(){0,1,0, 0,7,0, 0,0,6});
            // starting_schema.Add(new(){0,0,3, 2,0,0, 0,8,0});
            // starting_schema.Add(new(){0,6,0, 5,0,0, 0,0,9});
            // starting_schema.Add(new(){0,0,4, 0,0,0, 0,3,0});
            // starting_schema.Add(new(){0,0,0, 0,0,9, 7,0,0});
            
            Sudoku sudoku = new(starting_schema);
            
            //Console.Write(sudoku.ToString());
            sudoku.Fill();

            Console.Clear();
            Console.Write(sudoku.ToString());
        }
    }
}