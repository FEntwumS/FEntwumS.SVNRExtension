library ieee;                           -- Einbinden der ieee-Library
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity alu_n is
  port
    (
      clock_t       : in  std_logic;
      reset_init    : in  std_logic;
      cal           : in  std_logic;
      opcode        : in  std_logic_vector (7 downto 0);
      operand_a_in  : in  std_logic_vector (15 downto 0);
      operand_b_in  : in  std_logic_vector (15 downto 0);
      operand_c_out : out std_logic_vector (15 downto 0);

      greater_zero : out std_logic;
      equal_zero   : out std_logic;
      less_zero    : out std_logic
      );
end alu_n;

architecture verhalten of alu_n is

begin
  process (reset_init, clock_t, cal, opcode) is

    variable var_opcode : integer range 0 to 255;
    variable var_a      : integer range -32768 to 32767;
    variable var_b      : integer range -32768 to 32767;
    variable var_c      : integer range -32768 to 32767;

    variable var_std_logic_c : std_logic_vector (15 downto 0);

  begin
    if (rising_edge(clock_t)) then
      if(reset_init = '1') then
        var_a := 0;
        var_b := 0;
        var_c := 0;

      elsif (cal = '1') then

        var_opcode := to_integer (unsigned (opcode));
        var_a      := to_integer (signed (operand_a_in));
        var_b      := to_integer (signed (operand_b_in));

        case var_opcode is
          when 48 =>  --hex 30:  c = a + b (Teste 16#30# base#value#)
            var_c := var_a + var_b;

          when 49 =>                    --hex 31:  c = a - b
            var_c := var_a - var_b;

          when 50 =>                    --hex 32:  c = a * b
--          var_c := var_a * var_b;
            var_c := var_a + var_b;

          when 51 =>                    --hex 33:  c = a / b
--          var_c := var_a / var_b;
            var_c := var_a + var_b;

          when 52 =>  --hex 34:  c = a and b                              
            var_std_logic_c := operand_a_in and operand_b_in;
            var_c           := to_integer (signed (var_std_logic_c));

          when 53 =>  --hex 35:  c = a or b                               
            var_std_logic_c := operand_a_in or operand_b_in;
            var_c           := to_integer (signed (var_std_logic_c));

          when 54 =>  --hex 36:  c = not a                        
            var_std_logic_c := not operand_a_in;
            var_c           := to_integer (signed (var_std_logic_c));

          when 55 =>  --hex 37:  c = a xor b                              
            var_std_logic_c := operand_a_in xor operand_b_in;
            var_c           := to_integer (signed (var_std_logic_c));

          when 56 =>  --hex 38:  c = a + 1                                
            var_c := var_a + 1;

          when 57 =>  --hex 39:  c = a - 1                                
            var_c := var_a - 1;

          when 60 =>  --hex 3c:  c = a << b  (von rechts mit Nullen fuellen)

            var_std_logic_c := "0000000000000000";  -- Ergebnis mit Nullen vorbelegen

            case var_b is
              when 0 =>
                var_std_logic_c (15 downto 0) := operand_a_in(15 downto 0);
              when 1 =>
                var_std_logic_c (15 downto 1) := operand_a_in(14 downto 0);
              when 2 =>
                var_std_logic_c (15 downto 2) := operand_a_in(13 downto 0);
              when 3 =>
                var_std_logic_c (15 downto 3) := operand_a_in(12 downto 0);
              when 4 =>
                var_std_logic_c (15 downto 4) := operand_a_in(11 downto 0);
              when 5 =>
                var_std_logic_c (15 downto 5) := operand_a_in(10 downto 0);
              when 6 =>
                var_std_logic_c (15 downto 6) := operand_a_in(9 downto 0);
              when 7 =>
                var_std_logic_c (15 downto 7) := operand_a_in(8 downto 0);
              when 8 =>
                var_std_logic_c (15 downto 8) := operand_a_in(7 downto 0);
              when 9 =>
                var_std_logic_c (15 downto 9) := operand_a_in(6 downto 0);
              when 10 =>
                var_std_logic_c (15 downto 10) := operand_a_in(5 downto 0);
              when 11 =>
                var_std_logic_c (15 downto 11) := operand_a_in(4 downto 0);
              when 12 =>
                var_std_logic_c (15 downto 12) := operand_a_in(3 downto 0);
              when 13 =>
                var_std_logic_c (15 downto 13) := operand_a_in(2 downto 0);
              when 14 =>
                var_std_logic_c (15 downto 14) := operand_a_in(1 downto 0);
              when 15 =>
                var_std_logic_c (15 downto 15) := operand_a_in(0 downto 0);

              when others =>
                var_std_logic_c := "0000000000000000";
            end case;

            var_c := to_integer (signed (var_std_logic_c));



          when 61 =>  --hex 3d:  c = a >> b (von links mit Nullen fuellen)

            var_std_logic_c := "0000000000000000";  -- Ergebnis mit Nullen vorbelegen

            case var_b is
              when 0 =>
                var_std_logic_c (15 downto 0) := operand_a_in(15 downto 0);
              when 1 =>
                var_std_logic_c (14 downto 0) := operand_a_in(15 downto 1);
              when 2 =>
                var_std_logic_c (13 downto 0) := operand_a_in(15 downto 2);
              when 3 =>
                var_std_logic_c (12 downto 0) := operand_a_in(15 downto 3);
              when 4 =>
                var_std_logic_c (11 downto 0) := operand_a_in(15 downto 4);
              when 5 =>
                var_std_logic_c (10 downto 0) := operand_a_in(15 downto 5);
              when 6 =>
                var_std_logic_c (9 downto 0) := operand_a_in(15 downto 6);
              when 7 =>
                var_std_logic_c (8 downto 0) := operand_a_in(15 downto 7);
              when 8 =>
                var_std_logic_c (7 downto 0) := operand_a_in(15 downto 8);
              when 9 =>
                var_std_logic_c (6 downto 0) := operand_a_in(15 downto 9);
              when 10 =>
                var_std_logic_c (5 downto 0) := operand_a_in(15 downto 10);
              when 11 =>
                var_std_logic_c (4 downto 0) := operand_a_in(15 downto 11);
              when 12 =>
                var_std_logic_c (3 downto 0) := operand_a_in(15 downto 12);
              when 13 =>
                var_std_logic_c (2 downto 0) := operand_a_in(15 downto 13);
              when 14 =>
                var_std_logic_c (1 downto 0) := operand_a_in(15 downto 14);
              when 15 =>
                var_std_logic_c (0 downto 0) := operand_a_in(15 downto 15);

              when others =>
                var_std_logic_c := "0000000000000000";
            end case;

            var_c := to_integer (signed (var_std_logic_c));



          when others =>
            var_c := var_c;

        end case;

      end if;
    end if;

    operand_c_out <= std_logic_vector(to_signed(var_c, 16));

    if (var_c < 0) then
      less_zero <= '1';
    else
      less_zero <= '0';
    end if;


    if (var_c > 0) then
      greater_zero <= '1';
    else
      greater_zero <= '0';
    end if;


    if (var_c = 0) then
      equal_zero <= '1';
    else
      equal_zero <= '0';
    end if;




  end process;

end verhalten;
