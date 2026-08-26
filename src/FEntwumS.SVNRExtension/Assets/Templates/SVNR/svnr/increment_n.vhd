library IEEE;
use IEEE.STD_LOGIC_1164.all;
use IEEE.NUMERIC_STD.all;


entity increment_n is
  generic (SIZE : integer := 8);
  port (
    inp  : in  std_logic_vector(SIZE-1 downto 0);
    outp : out std_logic_vector(SIZE-1 downto 0)
    );
end increment_n;

architecture Behavioral of increment_n is

begin

  outp <= std_logic_vector(unsigned(inp) + to_unsigned(1, inp'length));

end Behavioral;
