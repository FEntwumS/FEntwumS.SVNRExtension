
library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;


entity slice_n is
    generic (
      DIN_WIDTH      : integer := 32;       -- Width of a Din input
      DIN_FROM       : integer := 8;       -- Din msb position to slice from
      DIN_DOWNTO     : integer := 0);       -- Din lsb position to slice to
    port (
      Din : in std_logic_vector (DIN_WIDTH-1 downto 0);
      Dout : out std_logic_vector ( DIN_FROM - DIN_DOWNTO downto 0)
      );
end slice_n;

architecture behavioral of slice_n is
begin

    Dout <= Din(DIN_FROM downto DIN_DOWNTO);
  
end behavioral;
