

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

-- Uncomment the following library declaration if using
-- arithmetic functions with Signed or Unsigned values
--use IEEE.NUMERIC_STD.ALL;

-- Uncomment the following library declaration if instantiating
-- any Xilinx leaf cells in this code.
--library UNISIM;
--use UNISIM.VComponents.all;

entity mux_4_n is
    Generic ( size : integer := 1);
    Port ( data3 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data2 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data1 : in STD_LOGIC_VECTOR (size-1 downto 0);
           data0 : in STD_LOGIC_VECTOR (size-1 downto 0);
           result : out STD_LOGIC_VECTOR (size-1 downto 0);
           sel : in STD_LOGIC_VECTOR (1 downto 0));
end mux_4_n;

architecture Behavioral of mux_4_n is

begin

    with sel select
        result  <=  data0 when "00",
                    data1 when "01",
                    data2 when "10",
                    data3 when "11",
                    (others => '0') when others;  


end Behavioral;
