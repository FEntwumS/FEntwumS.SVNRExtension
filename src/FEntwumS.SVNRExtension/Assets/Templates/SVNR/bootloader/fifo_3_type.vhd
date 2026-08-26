library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

package fifo_3_type is
	--constant INSTRUCTION_BUFFER_ADDRESS : integer := 4;  --bits wide
	--constant INSTRUCTION_BUFFER_DATA    : integer := 16; --bits wide
	constant c_width : Natural := 8; --1 byte
	constant c_depth : natural := 3; --3 davon
	type t_FIFO_DATA is array (0 to c_depth - 1) of std_logic_vector(c_width - 1 downto 0);
end package fifo_3_type;
